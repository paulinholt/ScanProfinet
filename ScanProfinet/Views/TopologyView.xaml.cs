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
            RenderSurfaceToPng(DiagramSurface, dlg.FileName);
            ExportHelper.OfferOpen(dlg.FileName);
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao exportar diagrama PNG", ex);
            MessageBox.Show($"Erro ao exportar imagem:\n{ex.Message}", "ScanProfinet",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Renderiza o diagrama inteiro (não só a parte visível) em PNG, ignorando o zoom
    /// da tela e usando fundo branco.
    /// </summary>
    private static void RenderSurfaceToPng(FrameworkElement surface, string path)
    {
        // Remove o zoom temporariamente para render 1:1 do tamanho real do canvas.
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

        // Fator de nitidez (limitado para não estourar memória em redes muito grandes).
        double scale = (w * 1.5) * (h * 1.5) > 40_000_000 ? 1.0 : 1.5;

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

        // Restaura o zoom.
        surface.LayoutTransform = oldTransform;
        surface.UpdateLayout();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }
}
