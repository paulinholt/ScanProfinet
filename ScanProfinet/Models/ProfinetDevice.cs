using CommunityToolkit.Mvvm.ComponentModel;

namespace ScanProfinet.Models;

/// <summary>
/// Dispositivo PROFINET descoberto via DCP (ou carregado de um snapshot salvo).
/// </summary>
public partial class ProfinetDevice : ObservableObject
{
    [ObservableProperty] private string _macAddress = "";
    [ObservableProperty] private string _ipAddress = "0.0.0.0";
    [ObservableProperty] private string _subnetMask = "0.0.0.0";
    [ObservableProperty] private string _gateway = "0.0.0.0";
    [ObservableProperty] private string _deviceName = "";
    [ObservableProperty] private string _deviceVendor = "";
    [ObservableProperty] private string _deviceRole = "";

    public ushort VendorId { get; set; }
    public ushort DeviceId { get; set; }
    public DateTime DiscoveredAt { get; set; } = DateTime.Now;

    /// <summary>Chave estável de identidade — o MAC é o único identificador físico confiável.</summary>
    public string Key => MacAddress.Trim().ToUpperInvariant();

    public bool HasIp => !string.IsNullOrWhiteSpace(IpAddress) && IpAddress != "0.0.0.0";
}

/// <summary>Informações de uma interface de rede local disponível para scan.</summary>
public class NetworkInterfaceInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public string IpAddress { get; set; } = "";

    public string Display =>
        string.IsNullOrWhiteSpace(IpAddress) || IpAddress == "0.0.0.0"
            ? Description
            : $"{Description}  —  {IpAddress}";
}
