using ScanProfinet.Models;

namespace ScanProfinet.Services;

/// <summary>
/// Compara uma rede de referência (snapshot salvo) com a rede atual (scan),
/// identificando dispositivos que saíram, entraram ou foram alterados.
/// A identidade do dispositivo é o MAC address (único e físico).
/// </summary>
public static class NetworkCompareService
{
    public static CompareSummary Compare(NetworkSnapshot reference, IEnumerable<ProfinetDevice> current,
                                         string comparisonName = "Rede atual", DateTime? comparisonDate = null)
    {
        var summary = new CompareSummary
        {
            ReferenceName = reference.Name,
            ReferenceDate = reference.CreatedAt,
            ComparisonName = comparisonName,
            ComparisonDate = comparisonDate
        };

        var refByMac = reference.Devices
            .GroupBy(d => d.Key)
            .ToDictionary(g => g.Key, g => g.First());

        var curList = current.ToList();
        var curByMac = curList
            .GroupBy(d => d.Key)
            .ToDictionary(g => g.Key, g => g.First());

        // 1) Percorre a referência: encontrados (mudaram ou não) vs. removidos.
        foreach (var refDev in refByMac.Values)
        {
            if (curByMac.TryGetValue(refDev.Key, out var curDev))
            {
                var diffs = DiffDevice(refDev, curDev);
                var row = new CompareRow
                {
                    MacAddress = refDev.MacAddress,
                    RefName = refDev.DeviceName, RefIp = refDev.IpAddress, RefVendor = refDev.DeviceVendor,
                    CurName = curDev.DeviceName, CurIp = curDev.IpAddress, CurVendor = curDev.DeviceVendor,
                    Differences = diffs,
                    Status = diffs.Count > 0 ? CompareStatus.Changed : CompareStatus.Unchanged
                };
                summary.Rows.Add(row);
                if (row.Status == CompareStatus.Changed) summary.Changed++; else summary.Unchanged++;
            }
            else
            {
                summary.Rows.Add(new CompareRow
                {
                    Status = CompareStatus.Removed,
                    MacAddress = refDev.MacAddress,
                    RefName = refDev.DeviceName, RefIp = refDev.IpAddress, RefVendor = refDev.DeviceVendor,
                    Differences = { $"Dispositivo ausente na rede atual (estava em '{reference.Name}')." }
                });
                summary.Removed++;
            }
        }

        // 2) Percorre a rede atual: os que não estão na referência entraram.
        foreach (var curDev in curByMac.Values)
        {
            if (refByMac.ContainsKey(curDev.Key)) continue;
            summary.Rows.Add(new CompareRow
            {
                Status = CompareStatus.Added,
                MacAddress = curDev.MacAddress,
                CurName = curDev.DeviceName, CurIp = curDev.IpAddress, CurVendor = curDev.DeviceVendor,
                Differences = { "Dispositivo novo — não existia na rede de referência." }
            });
            summary.Added++;
        }

        // Ordena por severidade: removidos → adicionados → alterados → ok.
        summary.Rows = summary.Rows
            .OrderBy(r => r.Status switch
            {
                CompareStatus.Removed => 0,
                CompareStatus.Added => 1,
                CompareStatus.Changed => 2,
                _ => 3
            })
            .ThenBy(r => r.Name)
            .ToList();

        return summary;
    }

    private static List<string> DiffDevice(ProfinetDevice reference, ProfinetDevice current)
    {
        var diffs = new List<string>();

        if (!SameText(reference.IpAddress, current.IpAddress))
            diffs.Add($"IP: {Show(reference.IpAddress)} → {Show(current.IpAddress)}");

        if (!SameText(reference.DeviceName, current.DeviceName))
            diffs.Add($"Nome: {Show(reference.DeviceName)} → {Show(current.DeviceName)}");

        if (!SameText(reference.Gateway, current.Gateway))
            diffs.Add($"Gateway: {Show(reference.Gateway)} → {Show(current.Gateway)}");

        if (!SameText(reference.SubnetMask, current.SubnetMask))
            diffs.Add($"Máscara: {Show(reference.SubnetMask)} → {Show(current.SubnetMask)}");

        return diffs;
    }

    private static bool SameText(string? a, string? b)
        => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Show(string? v) => string.IsNullOrWhiteSpace(v) ? "(vazio)" : v;
}
