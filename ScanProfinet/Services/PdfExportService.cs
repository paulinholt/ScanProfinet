using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace ScanProfinet.Services;

/// <summary>Monta o PDF a partir de tiles (PNG) já renderizados em alta resolução — um por página.</summary>
public static class PdfExportService
{
    public static void ExportDiagramTiles(IReadOnlyList<byte[]> tiles, string title, string path)
    {
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
                        row.ConstantItem(150).AlignRight().Text($"Página {pageNum}/{total}").FontSize(10);
                    });
                    p.Content().PaddingTop(6).Image(bytes).FitArea().UseOriginalImage();
                });
            }
        }).GeneratePdf(path);
    }
}
