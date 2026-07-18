using CommunityToolkit.Mvvm.ComponentModel;

namespace ScanProfinet.Models;

public enum MonitorState
{
    Unknown,
    Online,
    Offline,
    Unstable   // oscilando (flapping)
}

/// <summary>
/// Alvo de monitoramento (ping) — pode vir de um scan ou de um snapshot salvo.
/// </summary>
public partial class MonitorTarget : ObservableObject
{
    public string IpAddress { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string MacAddress { get; set; } = "";

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private MonitorState _state = MonitorState.Unknown;
    [ObservableProperty] private long _lastLatencyMs = -1;
    [ObservableProperty] private double _lossPercent;
    [ObservableProperty] private int _sent;
    [ObservableProperty] private int _received;
    [ObservableProperty] private int _transitions;          // nº de mudanças online<->offline
    [ObservableProperty] private DateTime? _lastChangeAt;
    [ObservableProperty] private string _lastEvent = "";

    // Histórico curto para o mini-gráfico de latência (sparkline).
    public System.Collections.ObjectModel.ObservableCollection<double> LatencyHistory { get; } = new();

    public string StateText => State switch
    {
        MonitorState.Online   => "ONLINE",
        MonitorState.Offline  => "OFFLINE",
        MonitorState.Unstable => "OSCILANDO",
        _                     => "—"
    };

    public string LatencyText => LastLatencyMs >= 0 ? $"{LastLatencyMs} ms" : "—";
    public string LossText => Sent > 0 ? $"{LossPercent:0.#}%" : "—";
}

/// <summary>Entrada de log de eventos de monitoramento.</summary>
public class MonitorEvent
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string IpAddress { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string EventType { get; set; } = "";   // OFFLINE, ONLINE, UNSTABLE
    public string Detail { get; set; } = "";

    public string TimestampText => Timestamp.ToString("dd/MM HH:mm:ss");
}
