using System.Net.NetworkInformation;
using ScanProfinet.Data;
using ScanProfinet.Models;

namespace ScanProfinet.Services;

/// <summary>
/// Monitora por ping (ICMP) uma lista de dispositivos e, opcionalmente, faz
/// re-scan DCP periódico para detectar dispositivos ADICIONADOS à rede.
/// Detecta:
///  • QUEDA   — o dispositivo parou de responder (falhas consecutivas);
///  • RETORNO — voltou a responder após uma queda;
///  • OSCILAÇÃO (flapping) — sobe/desce repetidamente numa janela de tempo;
///  • ADICIONADO — um dispositivo novo apareceu na rede (via re-scan DCP).
/// Todos os eventos vão para o banco, o log geral e o log dedicado de monitoramento.
/// </summary>
public class PingMonitorService
{
    private readonly SnapshotRepository _repo;
    private readonly Action<Action> _uiPost;
    private readonly object _sync = new();

    private CancellationTokenSource? _cts;
    private readonly List<Context> _contexts = new();
    private readonly HashSet<string> _knownMacs = new(StringComparer.OrdinalIgnoreCase);

    // Parâmetros de ping
    public int IntervalMs { get; set; } = 2000;
    public int TimeoutMs { get; set; } = 1000;
    public int FailThreshold { get; set; } = 2;
    public int FlapWindowSeconds { get; set; } = 60;
    public int FlapThreshold { get; set; } = 4;
    public int HistoryLength { get; set; } = 40;

    // Parâmetros de re-scan (detecção de novos dispositivos)
    public bool DetectNewDevices { get; set; }
    public int ScanInterfaceIndex { get; set; } = -1;
    public int RescanIntervalMs { get; set; } = 20000;

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    /// <summary>Disparado (na UI) sempre que um evento é registrado.</summary>
    public event Action<MonitorEvent>? EventLogged;

    /// <summary>Disparado (na UI) quando um novo dispositivo passa a ser monitorado.</summary>
    public event Action<MonitorTarget>? TargetAdded;

    public PingMonitorService(SnapshotRepository repo, Action<Action> uiPost)
    {
        _repo = repo;
        _uiPost = uiPost;
    }

    /// <param name="targets">Dispositivos a pingar.</param>
    /// <param name="baselineMacs">MACs conhecidos da rede (base para detectar novos).</param>
    public void Start(IEnumerable<MonitorTarget> targets, IEnumerable<string> baselineMacs)
    {
        Stop();
        lock (_sync)
        {
            _contexts.Clear();
            _knownMacs.Clear();
            foreach (var m in baselineMacs)
                if (!string.IsNullOrWhiteSpace(m)) _knownMacs.Add(m.Trim());

            foreach (var t in targets)
            {
                t.State = MonitorState.Unknown;
                t.Sent = t.Received = t.Transitions = 0;
                t.LastLatencyMs = -1;
                if (!string.IsNullOrWhiteSpace(t.MacAddress)) _knownMacs.Add(t.MacAddress.Trim());
                _contexts.Add(new Context(t));
            }
        }
        if (_contexts.Count == 0 && !DetectNewDevices) return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(() => PingLoopAsync(token), token);

        if (DetectNewDevices && ScanInterfaceIndex >= 0)
            _ = Task.Run(() => RescanLoopAsync(token), token);

        AppLog.MonitorLog($"INICIO — {_contexts.Count} alvo(s), ping {IntervalMs}ms" +
            (DetectNewDevices ? $", re-scan DCP {RescanIntervalMs / 1000}s" : ", sem re-scan"));
    }

    public void Stop()
    {
        if (_cts == null) return;
        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
        AppLog.MonitorLog("FIM — monitoramento parado.");
    }

    // ===================== Loop de ping =====================

    private async Task PingLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Context[] ctxs;
            lock (_sync) ctxs = _contexts.ToArray();

            try { await Task.WhenAll(ctxs.Select(PingOnceAsync)); } catch { }

            try { await Task.Delay(IntervalMs, token); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task PingOnceAsync(Context ctx)
    {
        long latency = -1;
        bool reachable = false;
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ctx.Target.IpAddress, TimeoutMs);
            reachable = reply.Status == IPStatus.Success;
            if (reachable) latency = reply.RoundtripTime;
        }
        catch { reachable = false; }

        _uiPost(() => ApplyResult(ctx, reachable, latency));
    }

    private void ApplyResult(Context ctx, bool reachable, long latency)
    {
        var t = ctx.Target;
        t.Sent++;
        if (reachable)
        {
            t.Received++;
            t.LastLatencyMs = latency;
            ctx.ConsecutiveFails = 0;
            ctx.ConsecutiveOk++;
        }
        else
        {
            t.LastLatencyMs = -1;
            ctx.ConsecutiveOk = 0;
            ctx.ConsecutiveFails++;
        }
        t.LossPercent = t.Sent > 0 ? (1.0 - (double)t.Received / t.Sent) * 100.0 : 0;

        t.LatencyHistory.Add(reachable ? latency : -1);
        while (t.LatencyHistory.Count > HistoryLength) t.LatencyHistory.RemoveAt(0);

        bool? raw = null;
        if (ctx.ConsecutiveOk >= 1) raw = true;
        else if (ctx.ConsecutiveFails >= FailThreshold) raw = false;
        if (raw == null) return;

        bool up = raw.Value;
        if (ctx.LastUp.HasValue && ctx.LastUp.Value != up)
        {
            t.Transitions++;
            ctx.TransitionTimes.Add(DateTime.Now);
        }
        ctx.LastUp = up;

        var cutoff = DateTime.Now.AddSeconds(-FlapWindowSeconds);
        ctx.TransitionTimes.RemoveAll(ts => ts < cutoff);
        bool flapping = ctx.TransitionTimes.Count >= FlapThreshold;

        MonitorState newState = flapping ? MonitorState.Unstable
                              : up ? MonitorState.Online
                              : MonitorState.Offline;

        if (newState != t.State)
        {
            var previous = t.State;
            t.State = newState;
            t.LastChangeAt = DateTime.Now;
            LogTransition(t, previous, newState);
        }
    }

    private void LogTransition(MonitorTarget t, MonitorState previous, MonitorState current)
    {
        if (previous == MonitorState.Unknown && current == MonitorState.Online) { t.LastEvent = "Online"; return; }

        string type, detail;
        switch (current)
        {
            case MonitorState.Offline:
                type = "QUEDA";
                detail = $"Sem resposta há {FailThreshold} tentativas. Perda {t.LossPercent:0.#}%.";
                break;
            case MonitorState.Unstable:
                type = "OSCILANDO";
                detail = $"{t.Transitions} transições; {FlapThreshold}+ em {FlapWindowSeconds}s. Conexão instável.";
                break;
            case MonitorState.Online:
                type = "RETORNO";
                detail = previous == MonitorState.Offline
                    ? $"Voltou a responder ({t.LastLatencyMs} ms)."
                    : $"Conexão estabilizada ({t.LastLatencyMs} ms).";
                break;
            default:
                return;
        }
        RaiseEvent(t.IpAddress, t.DeviceName, type, detail);
        t.LastEvent = $"{DateTime.Now:HH:mm:ss} — {type}";
    }

    // ===================== Loop de re-scan DCP =====================

    private async Task RescanLoopAsync(CancellationToken token)
    {
        // Aguarda o primeiro intervalo antes do primeiro re-scan.
        while (!token.IsCancellationRequested)
        {
            try { await Task.Delay(RescanIntervalMs, token); }
            catch (TaskCanceledException) { break; }
            if (token.IsCancellationRequested) break;

            try
            {
                var found = await ProfinetDcpService.DiscoverAsync(ScanInterfaceIndex, 3000);
                foreach (var dev in found)
                {
                    var mac = dev.MacAddress.Trim();
                    bool isNew;
                    lock (_sync) isNew = !_knownMacs.Contains(mac);
                    if (!isNew) continue;

                    lock (_sync) _knownMacs.Add(mac);
                    HandleNewDevice(dev);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Falha no re-scan DCP do monitor", ex);
            }
        }
    }

    private void HandleNewDevice(ProfinetDevice dev)
    {
        _uiPost(() =>
        {
            var target = new MonitorTarget
            {
                IpAddress = dev.IpAddress,
                DeviceName = string.IsNullOrWhiteSpace(dev.DeviceName) ? (dev.HasIp ? dev.IpAddress : dev.MacAddress) : dev.DeviceName,
                MacAddress = dev.MacAddress,
                IsSelected = true
            };

            // Se tiver IP, passa a ser pingado também.
            if (dev.HasIp)
            {
                lock (_sync) _contexts.Add(new Context(target));
                TargetAdded?.Invoke(target);
            }

            string ipInfo = dev.HasIp ? dev.IpAddress : "sem IP";
            RaiseEvent(dev.IpAddress, target.DeviceName, "ADICIONADO",
                $"Novo dispositivo na rede — {target.DeviceName} (IP {ipInfo}, MAC {dev.MacAddress}, fab. {(!string.IsNullOrWhiteSpace(dev.DeviceVendor) ? dev.DeviceVendor : "?")}).");
        });
    }

    // ===================== Registro de eventos =====================

    private void RaiseEvent(string ip, string name, string type, string detail)
    {
        var ev = new MonitorEvent { IpAddress = ip, DeviceName = name, EventType = type, Detail = detail };
        try { _repo.LogMonitorEvent(ev); } catch (Exception ex) { AppLog.Error("Falha ao gravar evento de monitor", ex); }
        AppLog.MonitorLog($"[{type}] {name} ({ip}) :: {detail}");
        EventLogged?.Invoke(ev);
    }

    private sealed class Context
    {
        public Context(MonitorTarget target) => Target = target;
        public MonitorTarget Target { get; }
        public int ConsecutiveFails;
        public int ConsecutiveOk;
        public bool? LastUp;
        public List<DateTime> TransitionTimes { get; } = new();
    }
}
