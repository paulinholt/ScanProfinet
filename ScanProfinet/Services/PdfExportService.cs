using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace ScanProfinet.Services;

/// <summary>Exporta um diagrama grande (bitmap) para um PDF paginado (tiles A4 paisagem).</summary>
public static class PdfExportService
{
    public static void ExportDiagram(BitmapSource full, string title, string path)
    {
        int W = full.PixelWidth, H = full.PixelHeight;

        // Área útil por página (~A4 paisagem a 150 dpi, com margens) + sobreposição entre páginas.
        const int pageW = 1560, pageH = 1050, overlap = 90;
        int stepX = pageW - overlap, stepY = pageH - overlap;

        var tiles = new List<byte[]>();
        for (int y = 0; y < H; y += stepY)
        {
            for (int x = 0; x < W; x += stepX)
            {
                int w = Math.Min(pageW, W - x);
                int h = Math.Min(pageH, H - y);
                if (w <= 0 || h <= 0) continue;
                var crop = new CroppedBitmap(full, new Int32Rect(x, y, w, h));
                tiles.Add(Encode(crop));
            }
        }
        if (tiles.Count == 0) tiles.Add(Encode(full));

        int total = tiles.Count;
        Document.Create(container =>
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                int pageNum = i + 1;
                byte[] bytes = tiles[i];
                container.Page(p =>
                {
                    p.Size(PageSizes.A4.Landscape());
                    p.Margin(14);
                    p.Header().Row(row =>
                    {
                        row.RelativeItem().Text(title).SemiBold().FontSize(11);
                        row.ConstantItem(140).AlignRight().Text($"Página {pageNum}/{total}").FontSize(10);
                    });
                    p.Content().PaddingTop(6).Image(bytes).FitArea();
                });
            }
        }).GeneratePdf(path);
    }

    private static byte[] Encode(BitmapSource bmp)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }
}
