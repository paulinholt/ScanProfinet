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
    private readonly PingMonitorService _monitor;

    [ObservableProperty] private string _statusText = "Carregue os dispositivos e inicie o monitoramento.";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private object? _selectedSource;   // "SCAN" (string) ou NetworkSnapshot
    [ObservableProperty] private int _intervalSeconds = 2;

    /// <summary>Itens da combo de fonte: "Rede atual (scan)" + snapshots salvos.</summary>
    public ObservableCollection<SourceOption> Sources { get; } = new();
    public ObservableCollection<MonitorTarget> Targets { get; } = new();
    public ObservableCollection<MonitorEvent> Events { get; } = new();

    public MonitorViewModel(SnapshotRepository repo, ScanViewModel scan)
    {
        _repo = repo;
        _scan = scan;
        _monitor = new PingMonitorService(repo, PostToUi);
        _monitor.EventLogged += OnEventLogged;
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
        Targets.Clear();
        var opt = SelectedSource as SourceOption ?? Sources.FirstOrDefault();
        if (opt == null) return;

        IEnumerable<ProfinetDevice> devices;
        if (opt.SnapshotId is long id)
        {
            var snap = _repo.LoadSnapshot(id);
            devices = snap?.Devices ?? Enumerable.Empty<ProfinetDevice>();
        }
        else
        {
            devices = _scan.Devices;
        }

        int skipped = 0;
        foreach (var d in devices)
        {
            if (!d.HasIp) { skipped++; continue; }   // sem IP não dá para pingar
            Targets.Add(new MonitorTarget
            {
                IpAddress = d.IpAddress,
                DeviceName = string.IsNullOrWhiteSpace(d.DeviceName) ? d.IpAddress : d.DeviceName,
                MacAddress = d.MacAddress
            });
        }

        StatusText = Targets.Count == 0
            ? "Nenhum dispositivo com IP válido para monitorar." + (skipped > 0 ? $" ({skipped} sem IP)" : "")
            : $"{Targets.Count} dispositivo(s) carregado(s)." + (skipped > 0 ? $" {skipped} ignorado(s) por não ter IP." : "");
    }

    [RelayCommand]
    private void Start()
    {
        var selected = Targets.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Carregue e selecione ao menos um dispositivo para monitorar.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _monitor.IntervalMs = Math.Max(1, IntervalSeconds) * 1000;
        _monitor.Start(selected);
        IsRunning = true;
        StatusText = $"Monitorando {selected.Count} dispositivo(s) a cada {IntervalSeconds}s...";
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

    private void OnEventLogged(MonitorEvent ev)
    {
        // Já chega na thread da UI (o serviço usa PostToUi).
        Events.Insert(0, ev);
        while (Events.Count > 500) Events.RemoveAt(Events.Count - 1);
    }

    public void OnClosing() => _monitor.Stop();

    public sealed record SourceOption(string Key, string Display, long? SnapshotId);
}
