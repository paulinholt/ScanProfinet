using System.Windows;
using ScanProfinet.ViewModels;

namespace ScanProfinet.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Garante que o monitor pare ao fechar.
        if (DataContext is MainViewModel vm)
            vm.Monitor.OnClosing();
        base.OnClosing(e);
    }
}
