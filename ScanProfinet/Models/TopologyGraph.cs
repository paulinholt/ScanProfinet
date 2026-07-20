using CommunityToolkit.Mvvm.ComponentModel;

namespace ScanProfinet.Models;

public enum NodeKind { Unknown, Controller, Switch, IODevice }

/// <summary>Nó (dispositivo) do desenho de topologia.</summary>
public partial class TopoNode : ObservableObject
{
    public string Name { get; init; } = "";
    public bool IsHub { get; set; }          // muitos vínculos (switch/CLP) → destaque
    public NodeKind Kind { get; set; }
    public string? ImageResource { get; set; }   // foto real do modelo, se houver
    public double Width { get; init; } = 168;
    public double Height { get; init; } = 54;

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;

    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    partial void OnXChanged(double value) => OnPropertyChanged(nameof(CenterX));
    partial void OnYChanged(double value) => OnPropertyChanged(nameof(CenterY));
}

/// <summary>Aresta (cabo) entre dois nós, com os rótulos das portas.</summary>
public partial class TopoEdge : ObservableObject
{
    private readonly TopoNode _a;
    private readonly TopoNode _b;

    public string PortA { get; }
    public string PortB { get; }

    public TopoEdge(TopoNode a, TopoNode b, string portA, string portB)
    {
        _a = a; _b = b; PortA = portA; PortB = portB;
        _a.PropertyChanged += (_, _) => Recalc();
        _b.PropertyChanged += (_, _) => Recalc();
    }

    public double X1 => _a.CenterX;
    public double Y1 => _a.CenterY;
    public double X2 => _b.CenterX;
    public double Y2 => _b.CenterY;
    public double LabelX => (X1 + X2) / 2;
    public double LabelY => (Y1 + Y2) / 2;

    public string PortLabel =>
        (string.IsNullOrWhiteSpace(PortA) && string.IsNullOrWhiteSpace(PortB)) ? "" : $"{PortA} · {PortB}";

    private void Recalc()
    {
        OnPropertyChanged(nameof(X1)); OnPropertyChanged(nameof(Y1));
        OnPropertyChanged(nameof(X2)); OnPropertyChanged(nameof(Y2));
        OnPropertyChanged(nameof(LabelX)); OnPropertyChanged(nameof(LabelY));
    }
}

public class TopologyGraph
{
    public List<TopoNode> Nodes { get; } = new();
    public List<TopoEdge> Edges { get; } = new();
    public double Width { get; set; }
    public double Height { get; set; }
}

/// <summary>Monta e posiciona o grafo a partir das ligações LLDP (layout em camadas, top-down).</summary>
public static class TopologyLayout
{
    public static TopologyGraph Build(IEnumerable<TopologyLink> links)
    {
        const double nodeW = 168, nodeH = 54, hGap = 40, vGapRow = 34, vGapLevel = 78, margin = 48, maxPerRow = 16;
        var graph = new TopologyGraph();

        var nodeMap = new Dictionary<string, TopoNode>(StringComparer.OrdinalIgnoreCase);
        var adj = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var edgePairs = new Dictionary<string, TopoEdge>();
        var roleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var imgMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        TopoNode Node(string name)
        {
            if (!nodeMap.TryGetValue(name, out var n))
            {
                n = new TopoNode { Name = name };
                nodeMap[name] = n;
                adj[name] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            return n;
        }

        foreach (var l in links)
        {
            if (string.IsNullOrWhiteSpace(l.LocalDevice) || string.IsNullOrWhiteSpace(l.NeighborDevice)) continue;
            if (string.Equals(l.LocalDevice, l.NeighborDevice, StringComparison.OrdinalIgnoreCase)) continue;

            if (!string.IsNullOrWhiteSpace(l.LocalRole)) roleMap[l.LocalDevice] = l.LocalRole;
            if (l.LocalVendorId != 0 || l.LocalDeviceId != 0)
                imgMap[l.LocalDevice] = DeviceImageCatalog.ResourceFor(l.LocalVendorId, l.LocalDeviceId);

            var a = Node(l.LocalDevice);
            var b = Node(l.NeighborDevice);
            adj[a.Name].Add(b.Name);
            adj[b.Name].Add(a.Name);

            var key = string.CompareOrdinal(a.Name, b.Name) <= 0 ? $"{a.Name}#{b.Name}" : $"{b.Name}#{a.Name}";
            if (!edgePairs.ContainsKey(key))
                edgePairs[key] = new TopoEdge(a, b, l.LocalPort, l.NeighborPort);
        }

        if (nodeMap.Count == 0) return graph;

        foreach (var n in nodeMap.Values)
        {
            n.IsHub = adj[n.Name].Count >= 3;
            var role = roleMap.TryGetValue(n.Name, out var rl) ? rl : "";
            n.ImageResource = imgMap.TryGetValue(n.Name, out var img) ? img : null;
            n.Kind = role.Contains("Controller", StringComparison.OrdinalIgnoreCase) ? NodeKind.Controller
                   : n.IsHub ? NodeKind.Switch
                   : !string.IsNullOrWhiteSpace(role) ? NodeKind.IODevice
                   : NodeKind.Unknown;
        }

        // Níveis via BFS a partir dos nós de maior grau (raiz provável = CLP/switch).
        var level = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var order = nodeMap.Keys.OrderByDescending(k => adj[k].Count).ToList();
        int nextBase = 0;
        foreach (var root in order)
        {
            if (level.ContainsKey(root)) continue;
            var q = new Queue<string>();
            q.Enqueue(root);
            level[root] = nextBase;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var nb in adj[c])
                    if (!level.ContainsKey(nb)) { level[nb] = level[c] + 1; q.Enqueue(nb); }
            }
            nextBase = level.Values.Count > 0 ? level.Values.Max() + 2 : nextBase + 2;
        }

        // Posiciona por nível; níveis com muitos nós quebram em várias sub-linhas.
        double maxWidth = 0, currentY = margin;
        var byLevel = nodeMap.Values.GroupBy(n => level.TryGetValue(n.Name, out var lv) ? lv : 0)
                             .OrderBy(g => g.Key);
        foreach (var g in byLevel)
        {
            var arr = g.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();
            int perRow = (int)Math.Min(arr.Count, maxPerRow);
            if (perRow < 1) perRow = 1;
            for (int i = 0; i < arr.Count; i++)
            {
                int col = i % perRow;
                int subRow = i / perRow;
                arr[i].X = margin + col * (nodeW + hGap);
                arr[i].Y = currentY + subRow * (nodeH + vGapRow);
            }
            int subRows = (int)Math.Ceiling(arr.Count / (double)perRow);
            currentY += subRows * (nodeH + vGapRow) + vGapLevel;
            double rowW = margin + perRow * (nodeW + hGap);
            if (rowW > maxWidth) maxWidth = rowW;
        }

        graph.Nodes.AddRange(nodeMap.Values);
        graph.Edges.AddRange(edgePairs.Values);
        graph.Width = maxWidth + margin;
        graph.Height = currentY + margin;
        return graph;
    }
}
