using SharpPcap;
using ScanProfinet.Models;

namespace ScanProfinet.Services;

/// <summary>
/// Descoberta e configuração de dispositivos PROFINET via protocolo DCP (Ethernet Layer 2).
/// Requer o driver Npcap instalado na máquina.
/// </summary>
public class ProfinetDcpService
{
    // === Constantes do protocolo DCP ===
    private const ushort EtherTypeProfinet = 0x8892;
    private static readonly byte[] DcpMulticastMac = { 0x01, 0x0E, 0xCF, 0x00, 0x00, 0x00 };

    private const ushort FrameIdIdentifyRequest = 0xFEFE;
    private const ushort FrameIdIdentifyResponse = 0xFEFF;
    private const ushort FrameIdGetSet = 0xFEFD;

    private const byte ServiceIdGet = 3;
    private const byte ServiceIdSet = 4;
    private const byte ServiceIdIdentify = 5;

    private const byte ServiceTypeRequest = 0;
    private const byte ServiceTypeResponse = 1;

    private const byte OptionIp = 0x01;
    private const byte SuboptionIpAddress = 0x02;

    private const byte OptionDeviceProp = 0x02;
    private const byte SuboptionVendor = 0x01;
    private const byte SuboptionNameOfStation = 0x02;
    private const byte SuboptionDeviceId = 0x03;
    private const byte SuboptionDeviceRole = 0x04;

    private const byte OptionControl = 0x05;
    private const byte SuboptionSignal = 0x03;

    private const byte OptionAll = 0xFF;
    private const byte SuboptionAll = 0xFF;

    /// <summary>Lista interfaces de rede utilizáveis para scan DCP (apenas as com MAC Ethernet válido).</summary>
    public static List<NetworkInterfaceInfo> GetNetworkInterfaces()
    {
        var result = new List<NetworkInterfaceInfo>();
        try
        {
            var devices = CaptureDeviceList.Instance;
            for (int i = 0; i < devices.Count; i++)
            {
                var dev = devices[i];
                var info = new NetworkInterfaceInfo
                {
                    Index = i,
                    Name = dev.Name,
                    Description = dev.Description ?? dev.Name
                };

                if (dev is SharpPcap.LibPcap.LibPcapLiveDevice libDev)
                {
                    var macBytes = libDev.MacAddress?.GetAddressBytes();
                    if (macBytes == null || macBytes.Length != 6)
                        continue; // sem MAC Ethernet → não serve para DCP (loopback, etc.)

                    info.MacAddress = BitConverter.ToString(macBytes).Replace("-", ":");

                    foreach (var addr in libDev.Addresses)
                    {
                        if (addr.Addr?.ipAddress?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            info.IpAddress = addr.Addr.ipAddress.ToString();
                            break;
                        }
                    }
                    result.Add(info);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha ao listar interfaces de rede", ex);
        }
        return result;
    }

    /// <summary>Verifica se o Npcap está instalado e funcional.</summary>
    public static bool IsNpcapAvailable()
    {
        try { return CaptureDeviceList.Instance.Count > 0; }
        catch { return false; }
    }

    /// <summary>Escaneia a rede PROFINET (DCP Identify All) e coleta as respostas.</summary>
    public static async Task<List<ProfinetDevice>> DiscoverAsync(int interfaceIndex, int timeoutMs = 3000, IProgress<string>? progress = null)
    {
        var devices = new List<ProfinetDevice>();
        var captureDevices = CaptureDeviceList.Instance;
        if (interfaceIndex < 0 || interfaceIndex >= captureDevices.Count)
            throw new ArgumentException($"Interface {interfaceIndex} inválida.");

        var dev = captureDevices[interfaceIndex];
        try
        {
            dev.Open(DeviceModes.Promiscuous, 1000);
            progress?.Report("Interface aberta. Enviando DCP Identify All...");

            byte[] srcMac = GetSrcMac(dev);
            dev.SendPacket(BuildIdentifyAllFrame(srcMac));

            progress?.Report($"Aguardando respostas ({timeoutMs / 1000.0:0.#}s)...");
            var endTime = DateTime.Now.AddMilliseconds(timeoutMs);

            await Task.Run(() =>
            {
                while (DateTime.Now < endTime)
                {
                    if (dev.GetNextPacket(out var capture) != GetPacketStatus.PacketRead) continue;
                    var data = capture.Data.ToArray();
                    if (!IsDcpFrame(data, FrameIdIdentifyResponse)) continue;
                    if (data[16] != ServiceIdIdentify || data[17] != ServiceTypeResponse) continue;

                    var device = ParseIdentifyResponse(data);
                    if (device != null && !devices.Any(d => d.Key == device.Key))
                        devices.Add(device);
                }
            });

            progress?.Report($"Scan concluído: {devices.Count} dispositivo(s).");
            AppLog.Info($"Scan DCP: {devices.Count} dispositivos na interface #{interfaceIndex}");
        }
        finally
        {
            if (dev.Started) dev.Close();
        }

        return devices.OrderBy(d => VersionSortKey(d.IpAddress)).ToList();
    }

    public static Task<bool> SetIpAsync(int interfaceIndex, string targetMac, string ip, string mask, string gateway, IProgress<string>? progress = null)
        => SendSetRequestAsync(interfaceIndex, targetMac, BuildIpBlock(ip, mask, gateway), "IP", progress);

    public static Task<bool> SetDeviceNameAsync(int interfaceIndex, string targetMac, string name, IProgress<string>? progress = null)
        => SendSetRequestAsync(interfaceIndex, targetMac, BuildNameBlock(name), "Device Name", progress);

    public static Task<bool> BlinkAsync(int interfaceIndex, string targetMac, IProgress<string>? progress = null)
        => SendSetRequestAsync(interfaceIndex, targetMac, BuildBlinkBlock(), "Blink LED", progress);

    // ===================== Helpers de frame =====================

    private static byte[] GetSrcMac(ICaptureDevice dev)
    {
        if (dev is SharpPcap.LibPcap.LibPcapLiveDevice libDev && libDev.MacAddress != null)
            return libDev.MacAddress.GetAddressBytes();
        return new byte[6];
    }

    private static bool IsDcpFrame(byte[] data, ushort expectedFrameId)
    {
        if (data.Length < 26) return false;
        ushort etherType = (ushort)((data[12] << 8) | data[13]);
        if (etherType != EtherTypeProfinet) return false;
        ushort frameId = (ushort)((data[14] << 8) | data[15]);
        return frameId == expectedFrameId;
    }

    private static byte[] BuildIdentifyAllFrame(byte[] srcMac)
    {
        var frame = new byte[60];
        Array.Copy(DcpMulticastMac, 0, frame, 0, 6);
        Array.Copy(srcMac, 0, frame, 6, 6);
        frame[12] = (byte)(EtherTypeProfinet >> 8);
        frame[13] = (byte)(EtherTypeProfinet & 0xFF);

        frame[14] = (byte)(FrameIdIdentifyRequest >> 8);
        frame[15] = (byte)(FrameIdIdentifyRequest & 0xFF);
        frame[16] = ServiceIdIdentify;
        frame[17] = ServiceTypeRequest;
        uint xid = (uint)Random.Shared.Next();
        frame[18] = (byte)(xid >> 24); frame[19] = (byte)(xid >> 16);
        frame[20] = (byte)(xid >> 8);  frame[21] = (byte)(xid & 0xFF);
        frame[22] = 0x00; frame[23] = 0x01; // response delay factor
        frame[24] = 0x00; frame[25] = 0x04; // data length = 4
        frame[26] = OptionAll;
        frame[27] = SuboptionAll;
        frame[28] = 0x00; frame[29] = 0x00; // block length = 0
        return frame;
    }

    private static byte[] BuildIpBlock(string ip, string mask, string gateway)
    {
        var block = new byte[18];
        block[0] = OptionIp;
        block[1] = SuboptionIpAddress;
        block[2] = 0x00; block[3] = 14;
        block[4] = 0x00; block[5] = 0x01; // qualifier: temporário
        Array.Copy(System.Net.IPAddress.Parse(ip).GetAddressBytes(), 0, block, 6, 4);
        Array.Copy(System.Net.IPAddress.Parse(mask).GetAddressBytes(), 0, block, 10, 4);
        Array.Copy(System.Net.IPAddress.Parse(gateway).GetAddressBytes(), 0, block, 14, 4);
        return block;
    }

    private static byte[] BuildNameBlock(string name)
    {
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(name.ToLowerInvariant());
        int blockLen = 2 + nameBytes.Length;
        int totalLen = 4 + blockLen;
        if (totalLen % 2 != 0) totalLen++;
        var block = new byte[totalLen];
        block[0] = OptionDeviceProp;
        block[1] = SuboptionNameOfStation;
        block[2] = (byte)(blockLen >> 8);
        block[3] = (byte)(blockLen & 0xFF);
        block[4] = 0x00; block[5] = 0x00;
        Array.Copy(nameBytes, 0, block, 6, nameBytes.Length);
        return block;
    }

    private static byte[] BuildBlinkBlock()
    {
        var block = new byte[8];
        block[0] = OptionControl;
        block[1] = SuboptionSignal;
        block[2] = 0x00; block[3] = 0x04;
        block[4] = 0x00; block[5] = 0x01;
        block[6] = 0x00; block[7] = 0x01;
        return block;
    }

    private static async Task<bool> SendSetRequestAsync(int interfaceIndex, string targetMac, byte[] dcpBlock, string operation, IProgress<string>? progress)
    {
        var captureDevices = CaptureDeviceList.Instance;
        if (interfaceIndex < 0 || interfaceIndex >= captureDevices.Count) return false;

        var dev = captureDevices[interfaceIndex];
        try
        {
            dev.Open(DeviceModes.Promiscuous, 1000);
            progress?.Report($"Enviando DCP Set ({operation})...");

            byte[] srcMac = GetSrcMac(dev);
            byte[] dstMac = ParseMacAddress(targetMac);

            int frameLen = Math.Max(60, 26 + dcpBlock.Length);
            var frame = new byte[frameLen];
            Array.Copy(dstMac, 0, frame, 0, 6);
            Array.Copy(srcMac, 0, frame, 6, 6);
            frame[12] = (byte)(EtherTypeProfinet >> 8);
            frame[13] = (byte)(EtherTypeProfinet & 0xFF);
            frame[14] = (byte)(FrameIdGetSet >> 8);
            frame[15] = (byte)(FrameIdGetSet & 0xFF);
            frame[16] = ServiceIdSet;
            frame[17] = ServiceTypeRequest;
            uint xid = (uint)Random.Shared.Next();
            frame[18] = (byte)(xid >> 24); frame[19] = (byte)(xid >> 16);
            frame[20] = (byte)(xid >> 8);  frame[21] = (byte)(xid & 0xFF);
            frame[22] = 0x00; frame[23] = 0x00;
            ushort dataLen = (ushort)dcpBlock.Length;
            frame[24] = (byte)(dataLen >> 8);
            frame[25] = (byte)(dataLen & 0xFF);
            Array.Copy(dcpBlock, 0, frame, 26, dcpBlock.Length);

            dev.SendPacket(frame);

            bool success = false;
            await Task.Run(() =>
            {
                var endTime = DateTime.Now.AddSeconds(3);
                while (DateTime.Now < endTime)
                {
                    if (dev.GetNextPacket(out var capture) != GetPacketStatus.PacketRead) continue;
                    var data = capture.Data.ToArray();
                    if (!IsDcpFrame(data, FrameIdGetSet)) continue;
                    if (data[16] == ServiceIdSet && data[17] == ServiceTypeResponse)
                    {
                        success = true;
                        break;
                    }
                }
            });

            progress?.Report(success ? $"{operation}: configurado com sucesso." : $"{operation}: sem confirmação do dispositivo.");
            AppLog.Info($"DCP Set {operation} → {targetMac}: {(success ? "OK" : "sem resposta")}");
            return success;
        }
        finally
        {
            if (dev.Started) dev.Close();
        }
    }

    // ===================== Parser =====================

    private static ProfinetDevice? ParseIdentifyResponse(byte[] data)
    {
        if (data.Length < 26) return null;

        var device = new ProfinetDevice
        {
            MacAddress = $"{data[6]:X2}:{data[7]:X2}:{data[8]:X2}:{data[9]:X2}:{data[10]:X2}:{data[11]:X2}"
        };

        int dataLength = (data[24] << 8) | data[25];
        int offset = 26;
        int endOffset = Math.Min(offset + dataLength, data.Length);

        while (offset + 4 <= endOffset)
        {
            byte option = data[offset];
            byte suboption = data[offset + 1];
            int blockLen = (data[offset + 2] << 8) | data[offset + 3];
            offset += 4;
            if (offset + blockLen > endOffset) break;

            try
            {
                if (option == OptionIp && suboption == SuboptionIpAddress && blockLen >= 14)
                {
                    int ipOffset = offset + 2;
                    device.IpAddress = $"{data[ipOffset]}.{data[ipOffset + 1]}.{data[ipOffset + 2]}.{data[ipOffset + 3]}";
                    device.SubnetMask = $"{data[ipOffset + 4]}.{data[ipOffset + 5]}.{data[ipOffset + 6]}.{data[ipOffset + 7]}";
                    device.Gateway = $"{data[ipOffset + 8]}.{data[ipOffset + 9]}.{data[ipOffset + 10]}.{data[ipOffset + 11]}";
                }
                else if (option == OptionDeviceProp && suboption == SuboptionVendor && blockLen >= 2)
                    device.DeviceVendor = System.Text.Encoding.ASCII.GetString(data, offset + 2, blockLen - 2).TrimEnd('\0');
                else if (option == OptionDeviceProp && suboption == SuboptionNameOfStation && blockLen >= 2)
                    device.DeviceName = System.Text.Encoding.ASCII.GetString(data, offset + 2, blockLen - 2).TrimEnd('\0');
                else if (option == OptionDeviceProp && suboption == SuboptionDeviceId && blockLen >= 6)
                {
                    device.VendorId = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
                    device.DeviceId = (ushort)((data[offset + 4] << 8) | data[offset + 5]);
                }
                else if (option == OptionDeviceProp && suboption == SuboptionDeviceRole && blockLen >= 3)
                {
                    byte role = data[offset + 2];
                    device.DeviceRole = role switch
                    {
                        0x01 => "IO Device",
                        0x02 => "IO Controller",
                        0x04 => "IO Multidevice",
                        0x08 => "IO Supervisor",
                        _ => $"0x{role:X2}"
                    };
                }
            }
            catch { /* bloco malformado — ignora */ }

            offset += blockLen;
            if (blockLen % 2 != 0) offset++;
        }
        return device;
    }

    private static byte[] ParseMacAddress(string mac)
    {
        var parts = mac.Replace("-", ":").Split(':');
        if (parts.Length != 6) throw new ArgumentException($"MAC inválido: {mac}");
        return parts.Select(p => Convert.ToByte(p, 16)).ToArray();
    }

    /// <summary>Ordena IPs numericamente (192.168.0.2 antes de 192.168.0.10).</summary>
    private static long VersionSortKey(string ip)
    {
        if (!System.Net.IPAddress.TryParse(ip, out var addr)) return long.MaxValue;
        var b = addr.GetAddressBytes();
        if (b.Length != 4) return long.MaxValue;
        return ((long)b[0] << 24) | ((long)b[1] << 16) | ((long)b[2] << 8) | b[3];
    }
}
