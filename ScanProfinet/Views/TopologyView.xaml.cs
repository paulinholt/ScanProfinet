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
            var tiles = RenderDiagramTiles(DiagramSurface);
            PdfExportService.ExportDiagramTiles(tiles, $"Topologia da rede — {DateTime.Now:dd/MM/yyyy HH:mm}", dlg.FileName);
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

    /// <summary>
    /// Fatia o diagrama em páginas (largura cheia, altura em proporção A4 paisagem) e
    /// renderiza CADA página direto do vetor em alta resolução — nítido, sem re-escalar bitmap.
    /// </summary>
    private static List<byte[]> RenderDiagramTiles(FrameworkElement surface)
    {
        var oldTransform = surface.LayoutTransform;
        surface.LayoutTransform = Transform.Identity;
        surface.UpdateLayout();

        var tiles = new List<byte[]>();
        try
        {
            double W = surface.ActualWidth, H = surface.ActualHeight;
            if (W < 1 || H < 1) throw new InvalidOperationException("Diagrama vazio.");

            const double aspect = 297.0 / 210.0;   // A4 paisagem
            const double px = 2.2;                  // densidade de render (nitidez)
            const double overlap = 70;
            double regionH = System.Math.Min(H, W / aspect);
            double step = System.Math.Max(60, regionH - overlap);

            for (double y = 0; y < H; y += step)
            {
                double rh = System.Math.Min(regionH, H - y);
                var bmp = RenderRegion(surface, new Rect(0, y, W, rh), px);
                if (TileHasContent(bmp)) tiles.Add(EncodePng(bmp));
            }
            if (tiles.Count == 0)
                tiles.Add(EncodePng(RenderRegion(surface, new Rect(0, 0, W, H), px)));
        }
        finally
        {
            surface.LayoutTransform = oldTransform;
            surface.UpdateLayout();
        }
        return tiles;
    }

    /// <summary>Renderiza uma região (em unidades do diagrama) direto do visual, com fundo branco.</summary>
    private static RenderTargetBitmap RenderRegion(FrameworkElement surface, Rect region, double px)
    {
        var vb = new VisualBrush(surface)
        {
            Viewbox = region,
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill
        };
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, region.Width, region.Height));
            dc.DrawRectangle(vb, null, new Rect(0, 0, region.Width, region.Height));
        }
        var rtb = new RenderTargetBitmap(
            System.Math.Max(1, (int)(region.Width * px)), System.Math.Max(1, (int)(region.Height * px)),
            96 * px, 96 * px, PixelFormats.Pbgra32);
        rtb.Render(dv);
        return rtb;
    }

    private static bool TileHasContent(BitmapSource bmp)
    {
        int w = bmp.PixelWidth, h = bmp.PixelHeight;
        if (w < 1 || h < 1) return false;
        int stride = w * 4;
        var row = new byte[stride];
        for (int y = 0; y < h; y += 6)
        {
            try { bmp.CopyPixels(new Int32Rect(0, y, w, 1), row, stride, 0); } catch { return true; }
            for (int i = 0; i < stride; i += 16)
                if (row[i] < 244 || row[i + 1] < 244 || row[i + 2] < 244) return true;
        }
        return false;
    }

    private static byte[] EncodePng(BitmapSource bmp)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }
}
