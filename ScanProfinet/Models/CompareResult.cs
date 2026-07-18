namespace ScanProfinet.Models;

public enum CompareStatus
{
    /// <summary>Presente na referência e na rede atual, sem mudanças relevantes.</summary>
    Unchanged,
    /// <summary>Presente em ambas, mas IP/nome/gateway mudou.</summary>
    Changed,
    /// <summary>Estava na referência salva e NÃO está mais na rede — saiu da rede.</summary>
    Removed,
    /// <summary>Não estava na referência salva e apareceu na rede — entrou na rede.</summary>
    Added
}

/// <summary>
/// Linha do resultado de comparação entre uma rede salva (referência) e a rede atual.
/// </summary>
public class CompareRow
{
    public CompareStatus Status { get; set; }
    public string MacAddress { get; set; } = "";

    // Dados da referência (snapshot salvo)
    public string? RefName { get; set; }
    public string? RefIp { get; set; }
    public string? RefVendor { get; set; }

    // Dados atuais (scan)
    public string? CurName { get; set; }
    public string? CurIp { get; set; }
    public string? CurVendor { get; set; }

    /// <summary>Descrição legível das diferenças (ex.: "IP mudou 192.168.0.10 → 192.168.0.55").</summary>
    public List<string> Differences { get; set; } = new();

    // Texto amigável para a UI
    public string StatusText => Status switch
    {
        CompareStatus.Removed   => "SAIU DA REDE",
        CompareStatus.Added     => "ENTROU NA REDE",
        CompareStatus.Changed   => "ALTERADO",
        _                       => "OK"
    };

    public string Vendor => CurVendor ?? RefVendor ?? "";
    public string Name => CurName ?? RefName ?? "";
    public string Ip => CurIp ?? RefIp ?? "";
    public string DifferencesText => string.Join("  •  ", Differences);
}

/// <summary>Resumo agregado da comparação.</summary>
public class CompareSummary
{
    public string ReferenceName { get; set; } = "";
    public DateTime ReferenceDate { get; set; }
    public int Removed { get; set; }
    public int Added { get; set; }
    public int Changed { get; set; }
    public int Unchanged { get; set; }
    public List<CompareRow> Rows { get; set; } = new();

    public bool HasDivergences => Removed > 0 || Added > 0 || Changed > 0;

    public string Headline => HasDivergences
        ? $"Divergências encontradas: {Removed} saíram, {Added} entraram, {Changed} alterados."
        : "Rede idêntica à referência salva. Nenhuma divergência.";
}
