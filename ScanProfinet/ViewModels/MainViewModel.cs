using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScanProfinet.Data;

namespace ScanProfinet.ViewModels;

public enum AppSection { Scan, Compare, Monitor }

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private AppSection _section = AppSection.Scan;

    public ScanViewModel Scan { get; }
    public CompareViewModel Compare { get; }
    public MonitorViewModel Monitor { get; }

    public string VersionText { get; }

    public MainViewModel()
    {
        Database.Initialize();
        var repo = new SnapshotRepository();

        Scan = new ScanViewModel(repo);
        Compare = new CompareViewModel(repo, Scan);
        Monitor = new MonitorViewModel(repo, Scan);

        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText = $"v{v?.Major}.{v?.Minor}.{v?.Build}";
    }

    // Flags para o realce do menu lateral (evita converters extras no XAML).
    public bool IsScan => Section == AppSection.Scan;
    public bool IsCompare => Section == AppSection.Compare;
    public bool IsMonitor => Section == AppSection.Monitor;

    partial void OnSectionChanged(AppSection value)
    {
        OnPropertyChanged(nameof(IsScan));
        OnPropertyChanged(nameof(IsCompare));
        OnPropertyChanged(nameof(IsMonitor));

        // Ao entrar em Comparar/Monitorar, atualiza as listas vindas do banco.
        if (value == AppSection.Compare) Compare.RefreshReferences();
        if (value == AppSection.Monitor) Monitor.RefreshSources();
    }

    [RelayCommand] private void GoScan() => Section = AppSection.Scan;
    [RelayCommand] private void GoCompare() => Section = AppSection.Compare;
    [RelayCommand] private void GoMonitor() => Section = AppSection.Monitor;
}
