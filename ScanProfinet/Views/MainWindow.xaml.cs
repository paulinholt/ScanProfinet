using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScanProfinet.Services;
using ScanProfinet.ViewModels;

namespace ScanProfinet.Views;

public partial class MainWindow : Window
{
    private TrayService? _tray;
    private bool _reallyExit;
    private bool _firstMinimizeNoticeShown;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _tray = new TrayService();
        _tray.OpenRequested += RestoreFromTray;
        _tray.ExitRequested += ExitApplication;

        if (DataContext is MainViewModel vm)
            vm.Notifications.Tray = _tray;
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;   // traz à frente
        Topmost = false;
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        if (!_firstMinimizeNoticeShown)
        {
            _firstMinimizeNoticeShown = true;
            _tray?.ShowBalloon("ScanProfinet continua rodando",
                "O monitoramento segue ativo em segundo plano. Clique no ícone da bandeja para reabrir.",
                Models.ToastType.Info);
        }
    }

    private void ExitApplication()
    {
        _reallyExit = true;
        (DataContext as MainViewModel)?.Monitor.OnClosing();
        _tray?.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // Minimizar => recolhe para a bandeja.
        if (WindowState == WindowState.Minimized)
            HideToTray();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Fechar no X => vai para a bandeja em vez de encerrar (monitoramento continua).
        if (!_reallyExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnClosing(e);
    }

    private void SavedList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject src &&
            ItemsControl.ContainerFromElement(SavedList, src) is ListBoxItem &&
            DataContext is MainViewModel vm)
        {
            vm.OpenSnapshotDetails(vm.SelectedSaved);
        }
    }

    private void SavedTopoList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject src &&
            ItemsControl.ContainerFromElement(SavedTopoList, src) is ListBoxItem &&
            DataContext is MainViewModel vm)
        {
            vm.OpenTopology(vm.SelectedSavedTopology);
        }
    }
}
