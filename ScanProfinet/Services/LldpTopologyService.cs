using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using ScanProfinet.Models;

namespace ScanProfinet.Services;

/// <summary>
/// Monta a topologia da rede (tabela de vizinhança) lendo a LLDP-MIB de cada
/// dispositivo via SNMP. Os quadros LLDP não atravessam switches, então a
/// descoberta é feita consultando cada device (como faz o PRONETA), e não por
/// captura passiva. Requer SNMP (comunidade "public") habilitado nos devices.
/// </summary>
public static class LldpTopologyService
{
    // OIDs da LLDP-MIB (IEEE 802.1AB)
    private const string OidLocSysName  = "1.0.8802.1.1.2.1.3.3.0";      // escalar
    private const string OidLocPortDesc = "1.0.8802.1.1.2.1.3.7.1.4";    // idx: localPortNum
    private const string OidRemChassisId= "1.0.8802.1.1.2.1.4.1.1.5";    // idx: tm.localPort.remIdx
    private const string OidRemPortId   = "1.0.8802.1.1.2.1.4.1.1.7";
    private const string OidRemPortDesc = "1.0.8802.1.1.2.1.4.1.1.8";
    private const string OidRemSysName  = "1.0.8802.1.1.2.1.4.1.1.9";

    private const int SnmpParallelism = 16;

    /// <summary>Descobre dispositivos por DCP e consulta a topologia LLDP via SNMP.</summary>
    public static async Task<TopologyResult> DiscoverAsync(int interfaceIndex, int dcpTimeoutMs, int snmpTimeoutMs, IProgress<string>? progress = null)
    {
        var result = new TopologyResult();

        progress?.Report("Descobrindo dispositivos (DCP)...");
        var devices = await ProfinetDcpService.DiscoverAsync(interfaceIndex, dcpTimeoutMs, progress);
        result.DevicesScanned = devices.Count;

        var withIp = devices.Where(d => d.HasIp).ToList();
        result.DevicesWithoutIp = devices.Count - withIp.Count;
        if (withIp.Count == 0) return result;

        progress?.Report($"Consultando LLDP via SNMP em {withIp.Count} dispositivo(s)...");

        int done = 0, answered = 0;
        var bag = new System.Collections.Concurrent.ConcurrentBag<TopologyLink>();

        await Parallel.ForEachAsync(withIp,
            new ParallelOptions { MaxDegreeOfParallelism = SnmpParallelism },
            async (dev, _) =>
            {
                var links = await Task.Run(() => QueryDevice(dev, snmpTimeoutMs));
                if (links.Count > 0)
                {
                    Interlocked.Increment(ref answered);
                    foreach (var l in links) bag.Add(l);
                }
                int d = Interlocked.Increment(ref done);
                if (d % 10 == 0 || d == withIp.Count)
                    progress?.Report($"SNMP {d}/{withIp.Count}...");
            });

        result.DevicesAnswered = answered;
        result.Links.AddRange(bag
            .OrderBy(l => l.LocalDevice, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.LocalPort, StringComparer.OrdinalIgnoreCase));

        AppLog.Info($"Topologia LLDP: {result.Links.Count} ligações de {answered}/{withIp.Count} devices com SNMP.");
        return result;
    }

    private static List<TopologyLink> QueryDevice(ProfinetDevice dev, int timeout)
    {
        var links = new List<TopologyLink>();
        try
        {
            var ep = new IPEndPoint(IPAddress.Parse(dev.IpAddress), 161);
            var community = new OctetString("public");

            string localName = string.IsNullOrWhiteSpace(dev.DeviceName)
                ? (TryGetScalar(ep, community, OidLocSysName, timeout) ?? dev.IpAddress)
                : dev.DeviceName;

            // Mapa: número da porta local -> descrição da porta
            var locPorts = new Dictionary<uint, string>();
            foreach (var v in TryWalk(ep, community, OidLocPortDesc, timeout))
            {
                var suf = Suffix(v.Id, OidLocPortDesc);
                if (suf.Length >= 1) locPorts[suf[0]] = DecodeOctet(v.Data);
            }

            // Entradas remotas (vizinhos)
            var rem = new Dictionary<(uint port, uint idx), RemEntry>();
            RemEntry Get((uint, uint) k) { if (!rem.TryGetValue(k, out var e)) { e = new RemEntry(); rem[k] = e; } return e; }

            foreach (var v in TryWalk(ep, community, OidRemSysName, timeout))
            {
                var s = Suffix(v.Id, OidRemSysName);
                if (s.Length >= 3) Get((s[1], s[2])).SysName = DecodeOctet(v.Data);
            }
            foreach (var v in TryWalk(ep, community, OidRemPortId, timeout))
            {
                var s = Suffix(v.Id, OidRemPortId);
                if (s.Length >= 3) Get((s[1], s[2])).PortId = DecodeOctet(v.Data);
            }
            foreach (var v in TryWalk(ep, community, OidRemPortDesc, timeout))
            {
                var s = Suffix(v.Id, OidRemPortDesc);
                if (s.Length >= 3) Get((s[1], s[2])).PortDesc = DecodeOctet(v.Data);
            }
            foreach (var v in TryWalk(ep, community, OidRemChassisId, timeout))
            {
                var s = Suffix(v.Id, OidRemChassisId);
                if (s.Length >= 3) Get((s[1], s[2])).ChassisId = DecodeOctet(v.Data);
            }

            foreach (var kv in rem)
            {
                var e = kv.Value;
                string neighbor = !string.IsNullOrWhiteSpace(e.SysName) ? e.SysName
                                : !string.IsNullOrWhiteSpace(e.ChassisId) ? e.ChassisId : "?";
                string neighborPort = !string.IsNullOrWhiteSpace(e.PortDesc) ? e.PortDesc
                                    : !string.IsNullOrWhiteSpace(e.PortId) ? e.PortId : "?";
                links.Add(new TopologyLink
                {
                    LocalDevice = localName,
                    LocalIp = dev.IpAddress,
                    LocalMac = dev.MacAddress,
                    LocalPort = locPorts.TryGetValue(kv.Key.port, out var p) && !string.IsNullOrWhiteSpace(p) ? p : $"porta {kv.Key.port}",
                    NeighborDevice = neighbor,
                    NeighborPort = neighborPort
                });
            }
        }
        catch { /* device sem SNMP/LLDP ou inacessível */ }
        return links;
    }

    // ===================== Helpers SNMP =====================

    private static IList<Variable> TryWalk(IPEndPoint ep, OctetString community, string oid, int timeout)
    {
        var list = new List<Variable>();
        try
        {
            Messenger.Walk(VersionCode.V1, ep, community, new ObjectIdentifier(oid), list, timeout, WalkMode.WithinSubtree);
        }
        catch { /* sem resposta */ }
        return list;
    }

    private static string? TryGetScalar(IPEndPoint ep, OctetString community, string oid, int timeout)
    {
        try
        {
            var res = Messenger.Get(VersionCode.V1, ep, community,
                new List<Variable> { new Variable(new ObjectIdentifier(oid)) }, timeout);
            var v = res.FirstOrDefault();
            if (v?.Data is OctetString) return DecodeOctet(v.Data);
        }
        catch { }
        return null;
    }

    private static uint[] Suffix(ObjectIdentifier id, string baseOid)
    {
        var full = id.ToNumerical();
        int baseLen = baseOid.Split('.').Length;
        return full.Length > baseLen ? full.Skip(baseLen).ToArray() : Array.Empty<uint>();
    }

    private static string DecodeOctet(ISnmpData data)
    {
        if (data is OctetString os)
        {
            var raw = os.GetRaw();
            if (raw.Length == 0) return "";
            bool printable = raw.All(b => b >= 32 && b < 127);
            return printable
                ? System.Text.Encoding.ASCII.GetString(raw).Trim().TrimEnd('\0')
                : string.Join(":", raw.Select(b => b.ToString("X2")));
        }
        return data.ToString() ?? "";
    }

    private sealed class RemEntry
    {
        public string SysName = "";
        public string ChassisId = "";
        public string PortId = "";
        public string PortDesc = "";
    }
}
