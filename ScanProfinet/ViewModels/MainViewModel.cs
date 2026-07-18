using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScanProfinet.Data;
using ScanProfinet.Models;
using ScanProfinet.Views;

namespace ScanProfinet.ViewModels;

public enum AppSection { Scan, Compare, Monitor }

public partial class MainViewModel : ObservableObject
{
    private readonly SnapshotRepository _repo;

    [ObservableProperty] private AppSection _section = AppSection.Scan;
    [ObservableProperty] private NetworkSnapshot? _selectedSaved;

    public ScanViewModel Scan { get; }
    public CompareViewModel Compare { get; }
    public MonitorViewModel Monitor { get; }

    /// <summary>Redes salvas exibidas no painel lateral direito.</summary>
    public ObservableCollection<NetworkSnapshot> SavedNetworks { get; } = new();

    public string VersionText { get; }
    public bool HasSavedNetworks => SavedNetworks.Count > 0;

    public MainViewModel()
    {
        Database.Initialize();
        _repo = new SnapshotRepository();

        Scan = new ScanViewModel(_repo);
        Compare = new CompareViewModel(_repo, Scan);
        Monitor = new MonitorViewModel(_repo, Scan);

        // Quando uma rede é salva ou excluída, atualiza o painel direito.
        Scan.SnapshotsChanged += RefreshSavedNetworks;
        Compare.SnapshotsChanged += RefreshSavedNetworks;

        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText = $"v{v?.Major}.{v?.Minor}.{v?.Build}";

        RefreshSavedNetworks();
    }

    public void RefreshSavedNetworks()
    {
        SavedNetworks.Clear();
        foreach (var s in _repo.ListSnapshots())
            SavedNetworks.Add(s);
        OnPropertyChanged(nameof(HasSavedNetworks));
    }

    /// <summary>Abre o detalhe de uma rede salva (dispositivos mapeados + data).</summary>
    public void OpenSnapshotDetails(NetworkSnapshot? snapshot)
    {
        if (snapshot == null) return;
        var full = _repo.LoadSnapshot(snapshot.Id);
        if (full == null)
        {
            MessageBox.Show("Não foi possível carregar a rede salva.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dlg = new SnapshotDetailsDialog(full) { Owner = Application.Current.MainWindow };
        dlg.ShowDialog();
    }

    // Realce do menu lateral esquerdo.
    public bool IsScan => Section == AppSection.Scan;
    public bool IsCompare => Section == AppSection.Compare;
    public bool IsMonitor => Section == AppSection.Monitor;

    partial void OnSectionChanged(AppSection value)
    {
        OnPropertyChanged(nameof(IsScan));
        OnPropertyChanged(nameof(IsCompare));
        OnPropertyChanged(nameof(IsMonitor));

        if (value == AppSection.Compare) Compare.RefreshReferences();
        if (value == AppSection.Monitor) Monitor.RefreshSources();
    }

    [RelayCommand] private void GoScan() => Section = AppSection.Scan;
    [RelayCommand] private void GoCompare() => Section = AppSection.Compare;
    [RelayCommand] private void GoMonitor() => Section = AppSection.Monitor;

    [RelayCommand] private void OpenSelectedSaved() => OpenSnapshotDetails(SelectedSaved);
    [RelayCommand] private void RefreshSaved() => RefreshSavedNetworks();
}
