using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScanProfinet.ViewModels;

namespace ScanProfinet.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void SavedList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Ignora duplo clique fora de um item.
        if (e.OriginalSource is DependencyObject src &&
            ItemsControl.ContainerFromElement(SavedList, src) is ListBoxItem &&
            DataContext is MainViewModel vm)
        {
            vm.OpenSnapshotDetails(vm.SelectedSaved);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Garante que o monitor pare ao fechar.
        if (DataContext is MainViewModel vm)
            vm.Monitor.OnClosing();
        base.OnClosing(e);
    }
}
