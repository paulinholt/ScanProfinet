using System.Net.NetworkInformation;
using ScanProfinet.Data;
using ScanProfinet.Models;

namespace ScanProfinet.Services;

/// <summary>
/// Monitora por ping (ICMP) uma lista de dispositivos, detectando:
///  • QUEDA   — o dispositivo parou de responder (falhas consecutivas);
///  • RETORNO — voltou a responder após uma queda;
///  • OSCILAÇÃO (flapping) — sobe/desce repetidamente numa janela de tempo.
/// Todos os eventos são gravados no banco e no arquivo de log.
/// </summary>
public class PingMonitorService
{
    private readonly SnapshotRepository _repo;
    private readonly Action<Action> _uiPost;      // marshaller para a thread da UI
    private CancellationTokenSource? _cts;
    private readonly List<Context> _contexts = new();

    // Parâmetros ajustáveis
    public int IntervalMs { get; set; } = 2000;
    public int TimeoutMs { get; set; } = 1000;
    public int FailThreshold { get; set; } = 2;        // falhas consecutivas → OFFLINE
    public int FlapWindowSeconds { get; set; } = 60;   // janela de análise de oscilação
    public int FlapThreshold { get; set; } = 4;        // transições na janela → OSCILANDO
    public int HistoryLength { get; set; } = 40;       // pontos do mini-gráfico

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    /// <summary>Disparado (na thread da UI) sempre que um evento é registrado.</summary>
    public event Action<MonitorEvent>? EventLogged;

    public PingMonitorService(SnapshotRepository repo, Action<Action> uiPost)
    {
        _repo = repo;
        _uiPost = uiPost;
    }

    public void Start(IEnumerable<MonitorTarget> targets)
    {
        Stop();
        _contexts.Clear();
        foreach (var t in targets)
        {
            t.State = MonitorState.Unknown;
            t.Sent = t.Received = t.Transitions = 0;
            t.LastLatencyMs = -1;
            _contexts.Add(new Context(t));
        }
        if (_contexts.Count == 0) return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(() => LoopAsync(token), token);
        AppLog.Info($"Monitor iniciado: {_contexts.Count} alvo(s), intervalo {IntervalMs}ms.");
    }

    public void Stop()
    {
        if (_cts == null) return;
        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
        AppLog.Info("Monitor parado.");
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var round = _contexts.Select(PingOnceAsync).ToArray();
            try { await Task.WhenAll(round); } catch { /* alvos individuais tratam erro */ }

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

        // histórico para o sparkline (usa -1 como "sem resposta")
        t.LatencyHistory.Add(reachable ? latency : -1);
        while (t.LatencyHistory.Count > HistoryLength) t.LatencyHistory.RemoveAt(0);

        // ---- raw up/down com histerese por limiar de falhas ----
        bool? raw = null;
        if (ctx.ConsecutiveOk >= 1) raw = true;
        else if (ctx.ConsecutiveFails >= FailThreshold) raw = false;
        if (raw == null) return; // ainda não há certeza (ex.: 1ª falha antes do limiar)

        bool up = raw.Value;

        // ---- detecção de transição ----
        if (ctx.LastUp.HasValue && ctx.LastUp.Value != up)
        {
            t.Transitions++;
            ctx.TransitionTimes.Add(DateTime.Now);
        }
        ctx.LastUp = up;

        // poda a janela de oscilação
        var cutoff = DateTime.Now.AddSeconds(-FlapWindowSeconds);
        ctx.TransitionTimes.RemoveAll(ts => ts < cutoff);
        bool flapping = ctx.TransitionTimes.Count >= FlapThreshold;

        // ---- estado consolidado ----
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
        // Não loga a primeira definição de estado a partir de "Unknown" quando online (ruído inicial).
        if (previous == MonitorState.Unknown && current == MonitorState.Online) { t.LastEvent = "Online"; return; }

        string type;
        string detail;
        switch (current)
        {
            case MonitorState.Offline:
                type = "QUEDA";
                detail = $"Sem resposta há {FailThreshold} tentativas. Última latência conhecida indisponível. Perda {t.LossPercent:0.#}%.";
                break;
            case MonitorState.Unstable:
                type = "OSCILANDO";
                detail = $"{t.Transitions} transições detectadas; {FlapThreshold}+ em {FlapWindowSeconds}s. Conexão instável.";
                break;
            case MonitorState.Online:
                type = "RETORNO";
                detail = previous == MonitorState.Offline
                    ? $"Dispositivo voltou a responder ({t.LastLatencyMs} ms)."
                    : $"Conexão estabilizada ({t.LastLatencyMs} ms).";
                break;
            default:
                return;
        }

        var ev = new MonitorEvent
        {
            IpAddress = t.IpAddress,
            DeviceName = t.DeviceName,
            EventType = type,
            Detail = detail
        };
        t.LastEvent = $"{ev.TimestampText} — {type}";

        try { _repo.LogMonitorEvent(ev); } catch (Exception ex) { AppLog.Error("Falha ao gravar evento de monitor", ex); }
        AppLog.Warn($"MONITOR {type} :: {t.DeviceName} ({t.IpAddress}) :: {detail}");
        EventLogged?.Invoke(ev);
    }

    /// <summary>Estado interno por alvo (não exposto à UI).</summary>
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
