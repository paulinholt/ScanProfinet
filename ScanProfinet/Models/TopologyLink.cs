namespace ScanProfinet.Models;

/// <summary>
/// Uma ligação física descoberta via LLDP: "porta local do dispositivo → porta do vizinho".
/// </summary>
public class TopologyLink
{
    public string LocalDevice { get; set; } = "";
    public string LocalIp { get; set; } = "";
    public string LocalMac { get; set; } = "";
    public string LocalPort { get; set; } = "";
    public string NeighborDevice { get; set; } = "";
    public string NeighborPort { get; set; } = "";
    public string LocalRole { get; set; } = "";

    /// <summary>Chave para deduplicar ligações recíprocas (A↔B).</summary>
    public string PairKey
    {
        get
        {
            var a = $"{LocalDevice}|{LocalPort}";
            var b = $"{NeighborDevice}|{NeighborPort}";
            return string.CompareOrdinal(a, b) <= 0 ? $"{a}#{b}" : $"{b}#{a}";
        }
    }
}

/// <summary>Resultado do mapeamento de topologia.</summary>
public class TopologyResult
{
    public List<TopologyLink> Links { get; } = new();
    public int DevicesScanned { get; set; }
    public int DevicesWithoutIp { get; set; }
    public int DevicesAnswered { get; set; }   // responderam SNMP com dados LLDP
}
