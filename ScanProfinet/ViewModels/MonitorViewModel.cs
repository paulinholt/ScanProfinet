using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScanProfinet.Data;
using ScanProfinet.Models;
using ScanProfinet.Services;

namespace ScanProfinet.ViewModels;

public partial class MonitorViewModel : ObservableObject
{
    private readonly SnapshotRepository _repo;
    private readonly ScanViewModel _scan;
    private readonly NotificationService _notify;
    private readonly PingMonitorService _monitor;

    /// <summary>Máximo de dispositivos monitorados simultaneamente.</summary>
    public const int MaxTargets = 50;

    [ObservableProperty] private string _statusText = "Carregue os dispositivos e inicie o monitoramento.";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private object? _selectedSource;
    [ObservableProperty] private int _intervalSeconds = 2;
    [ObservableProperty] private int _selectedCount;

    public string SelectionText => $"{SelectedCount} de {Targets.Count} selecionado(s)  ·  máximo {MaxTargets}";
    public int MaxTargetsValue => MaxTargets;

    // Detecção de novos dispositivos (re-scan DCP)
    [ObservableProperty] private bool _detectNewDevices = true;
    [ObservableProperty] private int _selectedScanInterfaceIndex = -1;
    [ObservableProperty] private int _rescanSeconds = 20;

    public ObservableCollection<SourceOption> Sources { get; } = new();
    public ObservableCollection<MonitorTarget> Targets { get; } = new();
    public ObservableCollection<MonitorEvent> Events { get; } = new();

    // Reaproveita as interfaces já detectadas pela aba de Scan.
    public ObservableCollection<NetworkInterfaceInfo> Interfaces => _scan.Interfaces;
    public bool IsNpcapAvailable => _scan.IsNpcapAvailable;

    public MonitorViewModel(SnapshotRepository repo, ScanViewModel scan, NotificationService notify)
    {
        _repo = repo;
        _scan = scan;
        _notify = notify;
        _monitor = new PingMonitorService(repo, PostToUi);
        _monitor.EventLogged += OnEventLogged;
        _monitor.TargetAdded += OnTargetAdded;

        // Herda a interface selecionada na aba de Scan, se houver.
        SelectedScanInterfaceIndex = _scan.SelectedInterfaceIndex;
        RefreshSources();
        LoadRecentEvents();
    }

    private static void PostToUi(Action a)
    {
        var d = Application.Current?.Dispatcher;
        if (d == null || d.CheckAccess()) a();
        else d.Invoke(a);
    }

    public void RefreshSources()
    {
        var previous = (SelectedSource as SourceOption)?.Key;
        Sources.Clear();
        Sources.Add(new SourceOption("SCAN", "Rede atual (último scan)", null));
        foreach (var s in _repo.ListSnapshots())
            Sources.Add(new SourceOption($"SNAP:{s.Id}", s.Display, s.Id));
        SelectedSource = Sources.FirstOrDefault(x => x.Key == previous) ?? Sources[0];
        OnPropertyChanged(nameof(IsNpcapAvailable));
    }

    private void LoadRecentEvents()
    {
        Events.Clear();
        foreach (var ev in _repo.ListMonitorEvents(300))
            Events.Add(ev);
    }

    [RelayCommand]
    private void LoadTargets()
    {
        foreach (var t in Targets) t.PropertyChanged -= OnTargetPropertyChanged;
        Targets.Clear();
        var opt = SelectedSource as SourceOption ?? Sources.FirstOrDefault();
        if (opt == null) return;

        IEnumerable<ProfinetDevice> devices;
        if (opt.SnapshotId is long id)
            devices = _repo.LoadSnapshot(id)?.Devices ?? Enumerable.Empty<ProfinetDevice>();
        else
            devices = _scan.Devices;

        int skipped = 0;
        foreach (var d in devices)
        {
            if (!d.HasIp) { skipped++; continue; }
            var t = new MonitorTarget
            {
                IpAddress = d.IpAddress,
                DeviceName = string.IsNullOrWhiteSpace(d.DeviceName) ? d.IpAddress : d.DeviceName,
                MacAddress = d.MacAddress
            };
            t.PropertyChanged += OnTargetPropertyChanged;
            Targets.Add(t);
        }
        RecomputeSelected();

        StatusText = Targets.Count == 0
            ? "Nenhum dispositivo com IP válido para monitorar." + (skipped > 0 ? $" ({skipped} sem IP)" : "")
            : $"{Targets.Count} dispositivo(s) carregado(s). Marque até {MaxTargets} para monitorar." + (skipped > 0 ? $" {skipped} sem IP ignorado(s)." : "");
    }

    private void OnTargetPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonitorTarget.IsSelected)) RecomputeSelected();
    }

    private void RecomputeSelected()
    {
        SelectedCount = Targets.Count(t => t.IsSelected);
        OnPropertyChanged(nameof(SelectionText));
    }

    [RelayCommand]
    private void SelectFirstBatch()
    {
        int n = 0;
        foreach (var t in Targets)
            t.IsSelected = n++ < MaxTargets;
        RecomputeSelected();
        StatusText = $"{Math.Min(Targets.Count, MaxTargets)} primeiro(s) dispositivo(s) selecionado(s).";
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var t in Targets) t.IsSelected = false;
        RecomputeSelected();
    }

    [RelayCommand]
    private void Start()
    {
        var selected = Targets.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0 && !DetectNewDevices)
        {
            MessageBox.Show("Marque ao menos um dispositivo (use 'Selecionar 50'), ou ative a detecção de novos dispositivos.",
                "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (selected.Count > MaxTargets)
        {
            MessageBox.Show($"Você marcou {selected.Count} dispositivos. O máximo é {MaxTargets}.\n\n" +
                "Use 'Limpar' e depois 'Selecionar 50', ou desmarque alguns.",
                "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (DetectNewDevices && (!IsNpcapAvailable || SelectedScanInterfaceIndex < 0))
        {
            var r = MessageBox.Show(
                "A detecção de novos dispositivos precisa do Npcap e de uma placa de rede selecionada.\n\n" +
                "Deseja continuar apenas com o monitoramento por ping (sem detectar novos)?",
                "ScanProfinet", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            DetectNewDevices = false;
        }

        _monitor.IntervalMs = Math.Max(1, IntervalSeconds) * 1000;
        _monitor.DetectNewDevices = DetectNewDevices;
        _monitor.MaxPingTargets = MaxTargets;
        _monitor.RescanIntervalMs = Math.Max(5, RescanSeconds) * 1000;
        _monitor.ScanInterfaceIndex = DetectNewDevices && SelectedScanInterfaceIndex >= 0
            ? Interfaces[SelectedScanInterfaceIndex].Index : -1;

        var baseline = Targets.Select(t => t.MacAddress);
        _monitor.Start(selected, baseline);
        IsRunning = true;
        StatusText = DetectNewDevices
            ? $"Monitorando {selected.Count} dispositivo(s) + detecção de novos a cada {RescanSeconds}s."
            : $"Monitorando {selected.Count} dispositivo(s) a cada {IntervalSeconds}s.";
    }

    [RelayCommand]
    private void Stop()
    {
        _monitor.Stop();
        IsRunning = false;
        StatusText = "Monitoramento parado.";
    }

    [RelayCommand]
    private void ClearEvents()
    {
        if (MessageBox.Show("Limpar todo o histórico de eventos gravado?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        _repo.ClearMonitorEvents();
        Events.Clear();
    }

    private void OnTargetAdded(MonitorTarget target)
    {
        // Já vem na thread da UI (o serviço usa PostToUi).
        target.PropertyChanged += OnTargetPropertyChanged;
        Targets.Add(target);
        RecomputeSelected();
    }

    private void OnEventLogged(MonitorEvent ev)
    {
        Events.Insert(0, ev);
        while (Events.Count > 500) Events.RemoveAt(Events.Count - 1);

        // Notificações (bandeja + toast) conforme o tipo de evento.
        switch (ev.EventType)
        {
            case "QUEDA":
                _notify.Notify($"Device fora da rede: {ev.DeviceName}", $"{ev.IpAddress} parou de responder.", ToastType.Danger);
                break;
            case "ADICIONADO":
                _notify.Notify($"Novo device na rede: {ev.DeviceName}", ev.Detail, ToastType.Warning);
                break;
            case "RETORNO":
                _notify.Notify($"Device voltou: {ev.DeviceName}", $"{ev.IpAddress} respondendo novamente.", ToastType.Success, tray: false);
                break;
            case "OSCILANDO":
                _notify.Notify($"Conexão instável: {ev.DeviceName}", ev.Detail, ToastType.Warning, tray: false);
                break;
        }
    }

    public void OnClosing() => _monitor.Stop();

    public sealed record SourceOption(string Key, string Display, long? SnapshotId);
}
