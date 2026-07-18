using System.Windows;

namespace ScanProfinet.Views;

public partial class SaveSnapshotDialog : Window
{
    public string SnapshotName => NameBox.Text;
    public string? Notes => string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim();

    public SaveSnapshotDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Informe um nome para a rede.", "ScanProfinet", MessageBoxButton.OK, MessageBoxImage.Information);
            NameBox.Focus();
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
