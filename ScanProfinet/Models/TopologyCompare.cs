namespace ScanProfinet.Models;

/// <summary>Ficha resumida de uma topologia salva.</summary>
public class TopologySnapshotInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int LinkCount { get; set; }
    public string Display => $"{Name}   ({CreatedAt:dd/MM/yyyy HH:mm} · {LinkCount} ligações)";
}

/// <summary>Linha da comparação de topologia (chave = dispositivo + porta local).</summary>
public class TopologyCompareRow
{
    public CompareStatus Status { get; set; }
    public string LocalDevice { get; set; } = "";
    public string LocalPort { get; set; } = "";
    public string RefNeighbor { get; set; } = "";
    public string CurNeighbor { get; set; } = "";
    public string Detail { get; set; } = "";

    public string StatusText => Status switch
    {
        CompareStatus.Removed => "LIGAÇÃO SUMIU",
        CompareStatus.Added   => "LIGAÇÃO NOVA",
        CompareStatus.Changed => "CABO TROCADO",
        _                     => "OK"
    };
}

public class TopologyCompareSummary
{
    public string ReferenceName { get; set; } = "";
    public string ComparisonName { get; set; } = "";
    public int Removed { get; set; }
    public int Added { get; set; }
    public int Changed { get; set; }
    public int Unchanged { get; set; }
    public List<TopologyCompareRow> Rows { get; set; } = new();

    public bool HasDivergences => Removed > 0 || Added > 0 || Changed > 0;

    public string Headline => HasDivergences
        ? $"Divergências: {Changed} cabo(s) trocado(s), {Removed} ligação(ões) sumida(s), {Added} nova(s)."
        : "Topologia idêntica à referência. Nenhuma divergência de cabeamento.";

    public string ComparisonHeader => $"{ReferenceName}   →   {ComparisonName}";
}
