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
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Selecione uma rede salva como referência e escaneie a rede atual.";
    [ObservableProperty] private CompareSummary? _result;
    [ObservableProperty] private string _headline = "";
    [ObservableProperty] private bool _hasResult;

    public ObservableCollection<NetworkSnapshot> References { get; } = new();
    public ObservableCollection<CompareRow> Rows { get; } = new();

    public int CurrentDeviceCount => _scan.Devices.Count;

    public CompareViewModel(SnapshotRepository repo, ScanViewModel scan)
    {
        _repo = repo;
        _scan = scan;
        _scan.Devices.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CurrentDeviceCount));
        RefreshReferences();
    }

    public void RefreshReferences()
    {
        References.Clear();
        foreach (var s in _repo.ListSnapshots())
            References.Add(s);
        StatusText = References.Count == 0
            ? "Nenhuma rede salva ainda. Vá em 'Scan' e salve uma rede primeiro."
            : $"{References.Count} rede(s) salva(s) disponível(is) como referência.";
    }

    [RelayCommand]
    private async Task ScanNow()
    {
        // Reaproveita o scan da aba principal para obter a rede atual.
        await _scan.ScanNetworkCommand.ExecuteAsync(null);
        OnPropertyChanged(nameof(CurrentDeviceCount));
        StatusText = $"Rede atual: {_scan.Devices.Count} dispositivo(s). Clique em 'Comparar'.";
    }

    [RelayCommand]
    private void Compare()
    {
        if (SelectedReference == null)
        {
            MessageBox.Show("Selecione a rede de referência (salva) para comparar.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_scan.Devices.Count == 0)
        {
            MessageBox.Show("Escaneie a rede atual antes de comparar (botão 'Escanear rede atual').", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
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

            var summary = NetworkCompareService.Compare(reference, _scan.Devices);
            Result = summary;
            Headline = summary.Headline;
            Rows.Clear();
            foreach (var row in summary.Rows) Rows.Add(row);
            HasResult = true;
            StatusText = $"Comparação com '{reference.Name}' concluída.";
            AppLog.Info($"Compare '{reference.Name}': -{summary.Removed} +{summary.Added} ~{summary.Changed}");
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
    private void DeleteReference()
    {
        if (SelectedReference == null) return;
        if (MessageBox.Show($"Excluir a rede salva '{SelectedReference.Name}'?", "Confirmar exclusão",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _repo.DeleteSnapshot(SelectedReference.Id);
        RefreshReferences();
        Rows.Clear();
        HasResult = false;
    }
}
