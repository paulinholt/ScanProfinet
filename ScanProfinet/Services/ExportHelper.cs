using System.Diagnostics;
using System.Windows;

namespace ScanProfinet.Services;

/// <summary>Auxiliares comuns de exportação: escolher caminho e abrir o arquivo gerado.</summary>
public static class ExportHelper
{
    public static string? AskPath(string suggestedName)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Planilha Excel (*.xlsx)|*.xlsx",
            FileName = suggestedName,
            AddExtension = true,
            DefaultExt = ".xlsx"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public static void OfferOpen(string path)
    {
        if (MessageBox.Show("Exportado com sucesso.\nAbrir o arquivo agora?", "ScanProfinet",
                MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
            return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { AppLog.Error("Falha ao abrir arquivo exportado", ex); }
    }
}
