using System.Windows;
using ScanProfinet.Models;

namespace ScanProfinet.Views;

public partial class SnapshotDetailsDialog : Window
{
    public SnapshotDetailsDialog(NetworkSnapshot snapshot)
    {
        InitializeComponent();

        Title = $"Rede salva — {snapshot.Name}";
        TitleText.Text = snapshot.Name;
        MetaText.Text = $"Mapeada em {snapshot.CreatedAt:dd/MM/yyyy 'às' HH:mm}  ·  {snapshot.Devices.Count} dispositivo(s)";

        if (!string.IsNullOrWhiteSpace(snapshot.Notes))
        {
            NotesText.Text = snapshot.Notes;
            NotesCard.Visibility = Visibility.Visible;
        }

        DevicesGrid.ItemsSource = snapshot.Devices;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
