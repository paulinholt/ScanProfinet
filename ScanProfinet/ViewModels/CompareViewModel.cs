using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScanProfinet.Data;
using ScanProfinet.Models;
using ScanProfinet.Services;

namespace ScanProfinet.ViewModels;

public partial class CompareViewModel : ObservableObject
{
    private readonly SnapshotRepository _repo;
    private readonly ScanViewModel _scan;

    [ObservableProperty] private NetworkSnapshot? _selectedReference;
    [ObservableProperty] private CompareTarget? _selectedComparison;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Escolha a rede de referência e com o que comparar.";
    [ObservableProperty] private CompareSummary? _result;
    [ObservableProperty] private string _headline = "";
    [ObservableProperty] private bool _hasResult;

    public ObservableCollection<NetworkSnapshot> References { get; } = new();
    /// <summary>Opções do "Comparar com": rede atual (scan) + redes salvas.</summary>
    public ObservableCollection<CompareTarget> Comparisons { get; } = new();
    public ObservableCollection<CompareRow> Rows { get; } = new();

    public int CurrentDeviceCount => _scan.Devices.Count;

    /// <summary>Disparado quando uma rede salva é excluída.</summary>
    public event Action? SnapshotsChanged;

    public CompareViewModel(SnapshotRepository repo, ScanViewModel scan)
    {
        _repo = repo;
        _scan = scan;
        _scan.Devices.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CurrentDeviceCount));
        RefreshReferences();
    }

    public void RefreshReferences()
    {
        var prevRef = SelectedReference?.Id;
        var prevComp = SelectedComparison?.Key;

        References.Clear();
        Comparisons.Clear();
        Comparisons.Add(new CompareTarget("SCAN", "Rede atual (último scan)", null));

        var snaps = _repo.ListSnapshots();
        foreach (var s in snaps)
        {
            References.Add(s);
            Comparisons.Add(new CompareTarget($"SNAP:{s.Id}", s.Display, s.Id));
        }

        SelectedReference = References.FirstOrDefault(r => r.Id == prevRef) ?? References.FirstOrDefault();
        SelectedComparison = Comparisons.FirstOrDefault(c => c.Key == prevComp) ?? Comparisons.FirstOrDefault();

        StatusText = snaps.Count == 0
            ? "Nenhuma rede salva ainda. Vá em 'Scan da rede' e salve uma rede primeiro."
            : $"{snaps.Count} rede(s) salva(s). Escolha a referência e com o que comparar.";
    }

    [RelayCommand]
    private async Task ScanNow()
    {
        await _scan.ScanNetworkCommand.ExecuteAsync(null);
        OnPropertyChanged(nameof(CurrentDeviceCount));
        StatusText = $"Rede atual: {_scan.Devices.Count} dispositivo(s).";
    }

    [RelayCommand]
    private void Compare()
    {
        if (SelectedReference == null)
        {
            MessageBox.Show("Selecione a rede de referência (base) para comparar.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (SelectedComparison == null)
        {
            MessageBox.Show("Selecione com o que comparar (rede atual ou outra rede salva).", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            IsBusy = true;

            var reference = _repo.LoadSnapshot(SelectedReference.Id);
            if (reference == null)
            {
                MessageBox.Show("Não foi possível carregar a rede de referência.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Resolve o alvo da comparação: scan atual ou uma rede salva.
            IReadOnlyList<ProfinetDevice> compDevices;
            string compName;
            DateTime? compDate;

            if (SelectedComparison.SnapshotId is long compId)
            {
                if (compId == reference.Id)
                {
                    MessageBox.Show("A referência e a rede comparada são a mesma. Escolha redes diferentes.",
                        "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var compSnap = _repo.LoadSnapshot(compId);
                if (compSnap == null)
                {
                    MessageBox.Show("Não foi possível carregar a rede comparada.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                compDevices = compSnap.Devices;
                compName = compSnap.Name;
                compDate = compSnap.CreatedAt;
            }
            else
            {
                if (_scan.Devices.Count == 0)
                {
                    MessageBox.Show("Escaneie a rede atual antes de comparar (botão 'Escanear rede atual'), ou escolha outra rede salva.",
                        "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                compDevices = _scan.Devices.ToList();
                compName = "Rede atual (scan)";
                compDate = null;
            }

            var summary = NetworkCompareService.Compare(reference, compDevices, compName, compDate);
            Result = summary;
            Headline = summary.Headline;
            Rows.Clear();
            foreach (var row in summary.Rows) Rows.Add(row);
            HasResult = true;
            StatusText = $"Comparação concluída: {reference.Name} → {compName}.";
            AppLog.Info($"Compare '{reference.Name}' → '{compName}': -{summary.Removed} +{summary.Added} ~{summary.Changed}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha na comparação", ex);
            MessageBox.Show($"Erro ao comparar:\n{ex.Message}", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ExportExcel()
    {
        if (Result == null || Rows.Count == 0)
        {
            MessageBox.Show("Faça uma comparação antes de exportar.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var path = ExportHelper.AskPath($"ScanProfinet_comparacao_{DateTime.Now:yyyy-MM-dd_HHmm}.xlsx");
        if (path == null) return;
        try
        {
            ExcelExportService.ExportCompare(Result, path);
            ExportHelper.OfferOpen(path);
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao exportar comparação", ex);
            MessageBox.Show($"Erro ao exportar:\n{ex.Message}", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void DeleteReference()
    {
        if (SelectedReference == null) return;
        if (MessageBox.Show($"Excluir a rede salva '{SelectedReference.Name}'?", "Confirmar exclusão",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _repo.DeleteSnapshot(SelectedReference.Id);
        RefreshReferences();
        SnapshotsChanged?.Invoke();
        Rows.Clear();
        HasResult = false;
    }

    public sealed record CompareTarget(string Key, string Display, long? SnapshotId);
}
