namespace ScanProfinet.Models;

/// <summary>
/// Uma "fotografia" nomeada da rede PROFINET salva no banco (ex.: "RedeCliente1").
/// </summary>
public class NetworkSnapshot
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Dispositivos carregados (vazio na listagem leve; preenchido em LoadSnapshot).</summary>
    public List<ProfinetDevice> Devices { get; set; } = new();

    /// <summary>
    /// Contagem de dispositivos. Na listagem vem direto do COUNT do banco;
    /// quando os dispositivos estão carregados, reflete a lista.
    /// </summary>
    public int DeviceCount { get; set; }

    public string Display => $"{Name}   ({CreatedAt:dd/MM/yyyy HH:mm} · {DeviceCount} disp.)";
}
