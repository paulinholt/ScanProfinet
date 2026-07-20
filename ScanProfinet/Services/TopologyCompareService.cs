using ScanProfinet.Models;

namespace ScanProfinet.Services;

/// <summary>
/// Compara duas topologias LLDP pela chave (dispositivo + porta local),
/// destacando cabos trocados (o vizinho daquela porta mudou), ligações que
/// sumiram e ligações novas.
/// </summary>
public static class TopologyCompareService
{
    public static TopologyCompareSummary Compare(
        IEnumerable<TopologyLink> reference, IEnumerable<TopologyLink> current,
        string referenceName, string comparisonName)
    {
        var summary = new TopologyCompareSummary { ReferenceName = referenceName, ComparisonName = comparisonName };

        static string Key(TopologyLink l) => $"{l.LocalDevice.Trim()}|{l.LocalPort.Trim()}".ToLowerInvariant();
        static string Neighbor(TopologyLink l) => $"{l.NeighborDevice} ({l.NeighborPort})";

        var refByKey = reference.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
        var curByKey = current.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());

        foreach (var r in refByKey.Values)
        {
            var key = Key(r);
            if (curByKey.TryGetValue(key, out var c))
            {
                bool same = string.Equals(r.NeighborDevice?.Trim(), c.NeighborDevice?.Trim(), StringComparison.OrdinalIgnoreCase)
                         && string.Equals(r.NeighborPort?.Trim(), c.NeighborPort?.Trim(), StringComparison.OrdinalIgnoreCase);
                if (same) { summary.Unchanged++; continue; }

                summary.Changed++;
                summary.Rows.Add(new TopologyCompareRow
                {
                    Status = CompareStatus.Changed,
                    LocalDevice = r.LocalDevice, LocalPort = r.LocalPort,
                    RefNeighbor = Neighbor(r), CurNeighbor = Neighbor(c),
                    Detail = $"A porta '{r.LocalPort}' de '{r.LocalDevice}' era ligada a {Neighbor(r)} e agora está em {Neighbor(c)}."
                });
            }
            else
            {
                summary.Removed++;
                summary.Rows.Add(new TopologyCompareRow
                {
                    Status = CompareStatus.Removed,
                    LocalDevice = r.LocalDevice, LocalPort = r.LocalPort,
                    RefNeighbor = Neighbor(r), CurNeighbor = "—",
                    Detail = $"A ligação da porta '{r.LocalPort}' de '{r.LocalDevice}' (era {Neighbor(r)}) não existe mais."
                });
            }
        }

        foreach (var c in curByKey.Values)
        {
            if (refByKey.ContainsKey(Key(c))) continue;
            summary.Added++;
            summary.Rows.Add(new TopologyCompareRow
            {
                Status = CompareStatus.Added,
                LocalDevice = c.LocalDevice, LocalPort = c.LocalPort,
                RefNeighbor = "—", CurNeighbor = Neighbor(c),
                Detail = $"Nova ligação: porta '{c.LocalPort}' de '{c.LocalDevice}' → {Neighbor(c)}."
            });
        }

        summary.Rows = summary.Rows
            .OrderBy(r => r.Status switch { CompareStatus.Changed => 0, CompareStatus.Removed => 1, CompareStatus.Added => 2, _ => 3 })
            .ThenBy(r => r.LocalDevice, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return summary;
    }
}
