using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScanProfinet.Data;
using ScanProfinet.Models;
using ScanProfinet.Services;
using ScanProfinet.Views;

namespace ScanProfinet.ViewModels;

public enum AppSection { Scan, Compare, Monitor, Topology }

public partial class MainViewModel : ObservableObject
{
    private readonly SnapshotRepository _repo;

    [ObservableProperty] private AppSection _section = AppSection.Scan;
    [ObservableProperty] private NetworkSnapshot? _selectedSaved;

    public ScanViewModel Scan { get; }
    public CompareViewModel Compare { get; }
    public MonitorViewModel Monitor { get; }
    public TopologyViewModel Topology { get; }
    public NotificationService Notifications { get; }

    /// <summary>Redes salvas exibidas no painel lateral direito.</summary>
    public ObservableCollection<NetworkSnapshot> SavedNetworks { get; } = new();

    public string VersionText { get; }
    public bool HasSavedNetworks => SavedNetworks.Count > 0;

    public MainViewModel()
    {
        Database.Initialize();
        _repo = new SnapshotRepository();

        Notifications = new NotificationService(PostToUi);
        Scan = new ScanViewModel(_repo);
        Compare = new CompareViewModel(_repo, Scan);
        Monitor = new MonitorViewModel(_repo, Scan, Notifications);
        Topology = new TopologyViewModel(Scan);

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
    public bool IsTopology => Section == AppSection.Topology;

    partial void OnSectionChanged(AppSection value)
    {
        OnPropertyChanged(nameof(IsScan));
        OnPropertyChanged(nameof(IsCompare));
        OnPropertyChanged(nameof(IsMonitor));
        OnPropertyChanged(nameof(IsTopology));

        if (value == AppSection.Compare) Compare.RefreshReferences();
        if (value == AppSection.Monitor) Monitor.RefreshSources();
    }

    [RelayCommand] private void GoScan() => Section = AppSection.Scan;
    [RelayCommand] private void GoCompare() => Section = AppSection.Compare;
    [RelayCommand] private void GoMonitor() => Section = AppSection.Monitor;
    [RelayCommand] private void GoTopology() => Section = AppSection.Topology;

    [RelayCommand] private void OpenSelectedSaved() => OpenSnapshotDetails(SelectedSaved);
    [RelayCommand] private void RefreshSaved() => RefreshSavedNetworks();

    [RelayCommand] private void DismissToast(ToastNotification? toast)
    {
        if (toast != null) Notifications.Dismiss(toast);
    }

    private static void PostToUi(Action a)
    {
        var d = Application.Current?.Dispatcher;
        if (d == null || d.CheckAccess()) a();
        else d.Invoke(a);
    }
}
