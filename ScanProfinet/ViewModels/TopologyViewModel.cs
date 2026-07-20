using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScanProfinet.Models;
using ScanProfinet.Services;

namespace ScanProfinet.ViewModels;

public partial class TopologyViewModel : ObservableObject
{
    private readonly ScanViewModel _scan;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasMapped;
    [ObservableProperty] private string _statusText = "Mapeie a topologia para ver as ligações porta a porta (LLDP via SNMP).";
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private int _scanSeconds = 5;
    [ObservableProperty] private bool _dedupe = true;

    [ObservableProperty] private int _devicesScanned;
    [ObservableProperty] private int _devicesAnswered;
    [ObservableProperty] private int _linkCount;

    private readonly List<TopologyLink> _allLinks = new();

    public ObservableCollection<TopologyLink> Links { get; } = new();

    // Reaproveita a placa e o Npcap já detectados na aba Scan.
    public ObservableCollection<NetworkInterfaceInfo> Interfaces => _scan.Interfaces;
    public bool IsNpcapAvailable => _scan.IsNpcapAvailable;

    [ObservableProperty] private int _selectedInterfaceIndex = -1;

    public TopologyViewModel(ScanViewModel scan)
    {
        _scan = scan;
        SelectedInterfaceIndex = _scan.SelectedInterfaceIndex;
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnDedupeChanged(bool value) => ApplyFilter();

    private void ApplyFilter()
    {
        Links.Clear();
        var f = FilterText.Trim();

        IEnumerable<TopologyLink> src = _allLinks;
        if (Dedupe)
            src = src.GroupBy(l => l.PairKey).Select(g => g.First());

        foreach (var l in src)
        {
            if (string.IsNullOrEmpty(f)
                || l.LocalDevice.Contains(f, StringComparison.OrdinalIgnoreCase)
                || l.NeighborDevice.Contains(f, StringComparison.OrdinalIgnoreCase)
                || l.LocalPort.Contains(f, StringComparison.OrdinalIgnoreCase)
                || l.NeighborPort.Contains(f, StringComparison.OrdinalIgnoreCase)
                || l.LocalIp.Contains(f, StringComparison.OrdinalIgnoreCase))
            {
                Links.Add(l);
            }
        }
        LinkCount = Links.Count;
    }

    [RelayCommand]
    private async Task Map()
    {
        if (SelectedInterfaceIndex < 0)
        {
            MessageBox.Show("Selecione a placa de rede conectada à rede PROFINET.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsBusy = true;
        _allLinks.Clear();
        Links.Clear();
        LinkCount = 0;

        try
        {
            var iface = Interfaces[SelectedInterfaceIndex];
            var progress = new Progress<string>(m => StatusText = m);
            var result = await LldpTopologyService.DiscoverAsync(iface.Index, ScanSeconds * 1000, 1000, progress);

            _allLinks.AddRange(result.Links);
            DevicesScanned = result.DevicesScanned;
            DevicesAnswered = result.DevicesAnswered;
            HasMapped = true;
            ApplyFilter();

            StatusText = result.Links.Count == 0
                ? $"{result.DevicesScanned} dispositivo(s) encontrados, mas nenhum retornou vizinhança LLDP via SNMP. Verifique se o SNMP está habilitado nos devices e se o PC está na mesma sub-rede."
                : $"{LinkCount} ligação(ões) mapeada(s). {result.DevicesAnswered} de {result.DevicesScanned} dispositivos responderam SNMP.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao mapear topologia", ex);
            MessageBox.Show($"Erro ao mapear topologia:\n{ex.Message}", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = $"Erro: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
