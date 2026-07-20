using ClosedXML.Excel;
using ScanProfinet.Models;

namespace ScanProfinet.Services;

/// <summary>Exporta os resultados para planilhas Excel (.xlsx). Não requer Excel instalado.</summary>
public static class ExcelExportService
{
    private static readonly XLColor HeaderBg = XLColor.FromHtml("#0F1B2D");

    public static void ExportScan(IEnumerable<ProfinetDevice> devices, string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Dispositivos");
        var headers = new[] { "Nome do dispositivo", "Fabricante", "IP", "Máscara", "Gateway", "MAC", "Função", "IP atribuído" };
        WriteHeader(ws, headers);

        int r = 2;
        foreach (var d in devices)
        {
            ws.Cell(r, 1).Value = d.DeviceName;
            ws.Cell(r, 2).Value = d.DeviceVendor;
            ws.Cell(r, 3).Value = d.IpAddress;
            ws.Cell(r, 4).Value = d.SubnetMask;
            ws.Cell(r, 5).Value = d.Gateway;
            ws.Cell(r, 6).Value = d.MacAddress;
            ws.Cell(r, 7).Value = d.DeviceRole;
            ws.Cell(r, 8).Value = d.HasIp ? "Sim" : "Não";
            r++;
        }
        Finish(ws, headers.Length);
        wb.SaveAs(path);
    }

    public static void ExportCompare(CompareSummary summary, string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Comparação");

        ws.Cell(1, 1).Value = summary.ComparisonHeader;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = summary.Headline;
        ws.Cell(3, 1).Value = $"Saíram: {summary.Removed}   Entraram: {summary.Added}   Alterados: {summary.Changed}   OK: {summary.Unchanged}";

        var headers = new[] { "Situação", "Dispositivo", "Fabricante", "IP", "MAC", "Diferenças" };
        int headerRow = 5;
        WriteHeader(ws, headers, headerRow);

        int r = headerRow + 1;
        foreach (var row in summary.Rows)
        {
            ws.Cell(r, 1).Value = row.StatusText;
            ws.Cell(r, 2).Value = row.Name;
            ws.Cell(r, 3).Value = row.Vendor;
            ws.Cell(r, 4).Value = row.Ip;
            ws.Cell(r, 5).Value = row.MacAddress;
            ws.Cell(r, 6).Value = row.DifferencesText;
            r++;
        }
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(headerRow);
        wb.SaveAs(path);
    }

    public static void ExportTopology(IEnumerable<TopologyLink> links, string path)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Topologia");
        var headers = new[] { "Dispositivo", "Porta local", "Vizinho", "Porta do vizinho", "IP local", "MAC local" };
        WriteHeader(ws, headers);

        int r = 2;
        foreach (var l in links)
        {
            ws.Cell(r, 1).Value = l.LocalDevice;
            ws.Cell(r, 2).Value = l.LocalPort;
            ws.Cell(r, 3).Value = l.NeighborDevice;
            ws.Cell(r, 4).Value = l.NeighborPort;
            ws.Cell(r, 5).Value = l.LocalIp;
            ws.Cell(r, 6).Value = l.LocalMac;
            r++;
        }
        Finish(ws, headers.Length);
        wb.SaveAs(path);
    }

    // ===================== helpers =====================

    private static void WriteHeader(IXLWorksheet ws, string[] headers, int row = 1)
    {
        for (int c = 0; c < headers.Length; c++)
            ws.Cell(row, c + 1).Value = headers[c];
        var range = ws.Range(row, 1, row, headers.Length);
        range.Style.Fill.BackgroundColor = HeaderBg;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
    }

    private static void Finish(IXLWorksheet ws, int columns)
    {
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        try { ws.Range(1, 1, 1, columns).SetAutoFilter(); } catch { }
    }
}
