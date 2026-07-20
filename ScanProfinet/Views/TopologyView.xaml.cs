using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScanProfinet.Models;
using ScanProfinet.Services;
using ScanProfinet.ViewModels;

namespace ScanProfinet.Views;

public partial class TopologyView : UserControl
{
    public TopologyView() => InitializeComponent();

    private void NodeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TopoNode node)
        {
            double zoom = (DataContext as TopologyViewModel)?.Zoom ?? 1.0;
            if (zoom <= 0) zoom = 1.0;
            node.X += e.HorizontalChange / zoom;
            node.Y += e.VerticalChange / zoom;
        }
    }

    private void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TopologyViewModel vm || vm.Nodes.Count == 0)
        {
            MessageBox.Show("Mapeie a topologia antes de exportar o diagrama.", "ScanProfinet",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Imagem PNG (*.png)|*.png",
            FileName = $"ScanProfinet_topologia_{DateTime.Now:yyyy-MM-dd_HHmm}.png",
            AddExtension = true,
            DefaultExt = ".png"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var bmp = RenderSurface(DiagramSurface, 1.5);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = File.Create(dlg.FileName)) enc.Save(fs);
            ExportHelper.OfferOpen(dlg.FileName);
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao exportar diagrama PNG", ex);
            MessageBox.Show($"Erro ao exportar imagem:\n{ex.Message}", "ScanProfinet",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TopologyViewModel vm || vm.Nodes.Count == 0)
        {
            MessageBox.Show("Mapeie a topologia antes de exportar o diagrama.", "ScanProfinet",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Documento PDF (*.pdf)|*.pdf",
            FileName = $"ScanProfinet_topologia_{DateTime.Now:yyyy-MM-dd_HHmm}.pdf",
            AddExtension = true,
            DefaultExt = ".pdf"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var bmp = RenderSurface(DiagramSurface, 2.0);
            PdfExportService.ExportDiagram(bmp, $"Topologia da rede — {DateTime.Now:dd/MM/yyyy HH:mm}", dlg.FileName);
            ExportHelper.OfferOpen(dlg.FileName);
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao exportar diagrama PDF", ex);
            MessageBox.Show($"Erro ao exportar PDF:\n{ex.Message}", "ScanProfinet",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Renderiza o diagrama inteiro (não só a parte visível) em bitmap, ignorando o zoom
    /// da tela e usando fundo branco. Retorna o bitmap para salvar como PNG ou paginar em PDF.
    /// </summary>
    private static RenderTargetBitmap RenderSurface(FrameworkElement surface, double preferredScale)
    {
        var oldTransform = surface.LayoutTransform;
        surface.LayoutTransform = Transform.Identity;
        surface.UpdateLayout();

        double w = surface.ActualWidth, h = surface.ActualHeight;
        if (w < 1 || h < 1)
        {
            surface.LayoutTransform = oldTransform;
            surface.UpdateLayout();
            throw new InvalidOperationException("Diagrama vazio.");
        }

        // Limita para não estourar memória em redes muito grandes (teto alto p/ manter nitidez).
        double scale = preferredScale;
        while (scale > 1.0 && (w * scale) * (h * scale) > 150_000_000) scale -= 0.25;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
            var brush = new VisualBrush(surface) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
            dc.DrawRectangle(brush, null, new Rect(0, 0, w, h));
        }

        var rtb = new RenderTargetBitmap(
            (int)Math.Ceiling(w * scale), (int)Math.Ceiling(h * scale),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        rtb.Render(visual);

        surface.LayoutTransform = oldTransform;
        surface.UpdateLayout();
        return rtb;
    }
}
