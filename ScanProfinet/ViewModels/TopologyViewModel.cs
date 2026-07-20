using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScanProfinet.Data;
using ScanProfinet.Models;
using ScanProfinet.Services;
using ScanProfinet.Views;

namespace ScanProfinet.ViewModels;

public enum TopoView { Tabela, Diagrama, Comparacao }

public partial class TopologyViewModel : ObservableObject
{
    private readonly ScanViewModel _scan;
    private readonly SnapshotRepository _repo;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasMapped;
    [ObservableProperty] private string _statusText = "Mapeie a topologia para ver as ligações porta a porta (LLDP via SNMP).";
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private int _scanSeconds = 5;
    [ObservableProperty] private bool _dedupe = true;
    [ObservableProperty] private int _selectedInterfaceIndex = -1;

    [ObservableProperty] private int _devicesScanned;
    [ObservableProperty] private int _devicesAnswered;
    [ObservableProperty] private int _linkCount;

    // Modo de exibição
    [ObservableProperty] private TopoView _view = TopoView.Tabela;
    [ObservableProperty] private double _zoom = 1.0;

    // Diagrama
    [ObservableProperty] private double _canvasWidth = 800;
    [ObservableProperty] private double _canvasHeight = 600;

    // Comparação
    [ObservableProperty] private TopologySnapshotInfo? _selectedComparison;
    [ObservableProperty] private TopologyCompareSummary? _compareResult;
    [ObservableProperty] private bool _hasCompareResult;

    private readonly List<TopologyLink> _allLinks = new();

    public ObservableCollection<TopologyLink> Links { get; } = new();
    public ObservableCollection<TopoNode> Nodes { get; } = new();
    public ObservableCollection<TopoEdge> Edges { get; } = new();
    public ObservableCollection<TopologySnapshotInfo> SavedTopologies { get; } = new();
    public ObservableCollection<TopologyCompareRow> CompareRows { get; } = new();

    public ObservableCollection<NetworkInterfaceInfo> Interfaces => _scan.Interfaces;
    public bool IsNpcapAvailable => _scan.IsNpcapAvailable;

    public bool IsTable => View == TopoView.Tabela;
    public bool IsDiagram => View == TopoView.Diagrama;
    public bool IsCompareView => View == TopoView.Comparacao;
    public bool HasLinks => _allLinks.Count > 0;

    public TopologyViewModel(ScanViewModel scan, SnapshotRepository repo)
    {
        _scan = scan;
        _repo = repo;
        SelectedInterfaceIndex = _scan.SelectedInterfaceIndex;
        RefreshTopologies();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnDedupeChanged(bool value) { ApplyFilter(); BuildGraph(); }

    partial void OnViewChanged(TopoView value)
    {
        OnPropertyChanged(nameof(IsTable));
        OnPropertyChanged(nameof(IsDiagram));
        OnPropertyChanged(nameof(IsCompareView));
    }

    public void RefreshTopologies()
    {
        var prev = SelectedComparison?.Id;
        SavedTopologies.Clear();
        foreach (var t in _repo.ListTopologies()) SavedTopologies.Add(t);
        SelectedComparison = SavedTopologies.FirstOrDefault(t => t.Id == prev) ?? SavedTopologies.FirstOrDefault();
    }

    private IEnumerable<TopologyLink> Deduped()
        => Dedupe ? _allLinks.GroupBy(l => l.PairKey).Select(g => g.First()) : _allLinks;

    private void ApplyFilter()
    {
        Links.Clear();
        var f = FilterText.Trim();
        foreach (var l in Deduped())
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

    private void BuildGraph()
    {
        var graph = TopologyLayout.Build(Deduped());
        Nodes.Clear();
        Edges.Clear();
        foreach (var n in graph.Nodes) Nodes.Add(n);
        foreach (var e in graph.Edges) Edges.Add(e);
        CanvasWidth = Math.Max(800, graph.Width);
        CanvasHeight = Math.Max(500, graph.Height);
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
        Links.Clear(); Nodes.Clear(); Edges.Clear();
        HasCompareResult = false; CompareRows.Clear();
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
            OnPropertyChanged(nameof(HasLinks));
            ApplyFilter();
            BuildGraph();
            if (View == TopoView.Comparacao) View = TopoView.Tabela;

            StatusText = result.Links.Count == 0
                ? $"{result.DevicesScanned} dispositivo(s) encontrados, mas nenhum retornou vizinhança LLDP via SNMP. Verifique o SNMP nos devices e se o PC está na mesma sub-rede."
                : $"{LinkCount} ligação(ões) mapeada(s). {result.DevicesAnswered} de {result.DevicesScanned} dispositivos responderam SNMP.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao mapear topologia", ex);
            MessageBox.Show($"Erro ao mapear topologia:\n{ex.Message}", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = $"Erro: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SaveTopology()
    {
        if (_allLinks.Count == 0)
        {
            MessageBox.Show("Mapeie a topologia antes de salvar.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveSnapshotDialog { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _repo.SaveTopology(dlg.SnapshotName.Trim(), dlg.Notes, Deduped());
            RefreshTopologies();
            StatusText = $"Topologia salva como '{dlg.SnapshotName.Trim()}'.";
            MessageBox.Show($"Topologia '{dlg.SnapshotName.Trim()}' salva com sucesso.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao salvar topologia", ex);
            MessageBox.Show($"Erro ao salvar topologia:\n{ex.Message}", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CompareTopology()
    {
        if (SelectedComparison == null)
        {
            MessageBox.Show("Selecione uma topologia salva (referência) para comparar.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_allLinks.Count == 0)
        {
            MessageBox.Show("Mapeie a topologia atual antes de comparar (botão 'Mapear topologia').", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var refLinks = _repo.LoadTopologyLinks(SelectedComparison.Id);
            var summary = TopologyCompareService.Compare(refLinks, _allLinks, SelectedComparison.Name, "Topologia atual");
            CompareResult = summary;
            CompareRows.Clear();
            foreach (var row in summary.Rows) CompareRows.Add(row);
            HasCompareResult = true;
            View = TopoView.Comparacao;
            StatusText = $"Comparação de topologia: {SelectedComparison.Name} → atual.";
            AppLog.Info($"Compare topologia '{SelectedComparison.Name}': trocados {summary.Changed}, sumiram {summary.Removed}, novas {summary.Added}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao comparar topologia", ex);
            MessageBox.Show($"Erro ao comparar topologia:\n{ex.Message}", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void DeleteTopology()
    {
        if (SelectedComparison == null) return;
        if (MessageBox.Show($"Excluir a topologia salva '{SelectedComparison.Name}'?", "Confirmar exclusão",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _repo.DeleteTopology(SelectedComparison.Id);
        RefreshTopologies();
    }

    [RelayCommand] private void ShowTable() => View = TopoView.Tabela;
    [RelayCommand] private void ShowDiagram() => View = TopoView.Diagrama;
    [RelayCommand] private void ZoomIn() => Zoom = Math.Min(2.5, Zoom + 0.15);
    [RelayCommand] private void ZoomOut() => Zoom = Math.Max(0.3, Zoom - 0.15);
    [RelayCommand] private void ZoomReset() => Zoom = 1.0;
}
