namespace ScanProfinet.Models;

/// <summary>
/// De:para VendorID/DeviceID → imagem real do device (fotos embutidas no app,
/// como faz o PRONETA). Modelos sem imagem caem no ícone por tipo.
/// </summary>
public static class DeviceImageCatalog
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Siemens (VendorID 0x002A)
        { "002A_0510", "g120c.png" },        // SINAMICS G120C
        { "002A_0511", "g120c.png" },        // SINAMICS G120 CU240E-2 (mesma família)
        { "002A_0114", "s7-1500.png" },      // S7-1500
        { "002A_010D", "s7-1200.png" },      // S7-1200
        { "002A_0403", "simatic-hmi.png" },  // SIMATIC HMI
        { "002A_0A03", "scalance-w700.png" },// SCALANCE W-700
        { "002A_0313", "et200sp.png" },      // ET200SP
        { "002A_010B", "et200s.png" },       // ET200S CPU
        { "002A_0302", "et200s.png" },       // IM153 / ET200M (aproxima)
    };

    /// <summary>Retorna o nome do arquivo de imagem ou null se não houver.</summary>
    public static string? ResourceFor(ushort vendorId, ushort deviceId)
        => Map.TryGetValue($"{vendorId:X4}_{deviceId:X4}", out var f) ? f : null;
}
