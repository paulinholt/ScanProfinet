using Microsoft.Data.Sqlite;
using ScanProfinet.Models;

namespace ScanProfinet.Data;

/// <summary>
/// CRUD dos snapshots de rede e dos eventos de monitoramento.
/// </summary>
public class SnapshotRepository
{
    // ===================== Snapshots =====================

    public long SaveSnapshot(string name, string? notes, IEnumerable<ProfinetDevice> devices)
    {
        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO Snapshots (Name, Notes, CreatedAt) VALUES ($n, $o, $c); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$o", (object?)notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$c", DateTime.Now.ToString("O"));
            long id = (long)cmd.ExecuteScalar()!;

            foreach (var d in devices)
                InsertDevice(conn, tx, id, d);

            tx.Commit();
            return id;
        }
    }

    private static void InsertDevice(SqliteConnection conn, SqliteTransaction tx, long snapshotId, ProfinetDevice d)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"INSERT INTO SnapshotDevices
            (SnapshotId, MacAddress, IpAddress, SubnetMask, Gateway, DeviceName, DeviceVendor, DeviceRole, VendorId, DeviceId)
            VALUES ($s,$mac,$ip,$mask,$gw,$name,$vendor,$role,$vid,$did);";
        cmd.Parameters.AddWithValue("$s", snapshotId);
        cmd.Parameters.AddWithValue("$mac", d.MacAddress);
        cmd.Parameters.AddWithValue("$ip", d.IpAddress);
        cmd.Parameters.AddWithValue("$mask", d.SubnetMask);
        cmd.Parameters.AddWithValue("$gw", d.Gateway);
        cmd.Parameters.AddWithValue("$name", d.DeviceName);
        cmd.Parameters.AddWithValue("$vendor", d.DeviceVendor);
        cmd.Parameters.AddWithValue("$role", d.DeviceRole);
        cmd.Parameters.AddWithValue("$vid", d.VendorId);
        cmd.Parameters.AddWithValue("$did", d.DeviceId);
        cmd.ExecuteNonQuery();
    }

    public List<NetworkSnapshot> ListSnapshots()
    {
        var result = new List<NetworkSnapshot>();
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT s.Id, s.Name, s.Notes, s.CreatedAt, COUNT(d.Id)
            FROM Snapshots s
            LEFT JOIN SnapshotDevices d ON d.SnapshotId = s.Id
            GROUP BY s.Id
            ORDER BY s.CreatedAt DESC;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var snap = new NetworkSnapshot
            {
                Id = r.GetInt64(0),
                Name = r.GetString(1),
                Notes = r.IsDBNull(2) ? null : r.GetString(2),
                CreatedAt = DateTime.Parse(r.GetString(3)),
            };
            snap.DeviceCount = r.GetInt32(4);
            result.Add(snap);
        }
        return result;
    }

    public NetworkSnapshot? LoadSnapshot(long id)
    {
        using var conn = Database.Open();

        NetworkSnapshot? snap = null;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Name, Notes, CreatedAt FROM Snapshots WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                snap = new NetworkSnapshot
                {
                    Id = r.GetInt64(0),
                    Name = r.GetString(1),
                    Notes = r.IsDBNull(2) ? null : r.GetString(2),
                    CreatedAt = DateTime.Parse(r.GetString(3)),
                    Devices = new List<ProfinetDevice>()
                };
            }
        }
        if (snap == null) return null;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"SELECT MacAddress, IpAddress, SubnetMask, Gateway, DeviceName,
                                       DeviceVendor, DeviceRole, VendorId, DeviceId
                                FROM SnapshotDevices WHERE SnapshotId = $id ORDER BY IpAddress;";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                snap.Devices.Add(new ProfinetDevice
                {
                    MacAddress = r.GetString(0),
                    IpAddress = r.IsDBNull(1) ? "0.0.0.0" : r.GetString(1),
                    SubnetMask = r.IsDBNull(2) ? "0.0.0.0" : r.GetString(2),
                    Gateway = r.IsDBNull(3) ? "0.0.0.0" : r.GetString(3),
                    DeviceName = r.IsDBNull(4) ? "" : r.GetString(4),
                    DeviceVendor = r.IsDBNull(5) ? "" : r.GetString(5),
                    DeviceRole = r.IsDBNull(6) ? "" : r.GetString(6),
                    VendorId = r.IsDBNull(7) ? (ushort)0 : (ushort)r.GetInt32(7),
                    DeviceId = r.IsDBNull(8) ? (ushort)0 : (ushort)r.GetInt32(8),
                });
            }
        }
        snap.DeviceCount = snap.Devices.Count;
        return snap;
    }

    public void DeleteSnapshot(long id)
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Snapshots WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public bool NameExists(string name)
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM Snapshots WHERE Name = $n;";
        cmd.Parameters.AddWithValue("$n", name);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    // ===================== Eventos de monitoramento =====================

    public void LogMonitorEvent(MonitorEvent ev)
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO MonitorEvents (Timestamp, IpAddress, DeviceName, EventType, Detail)
                            VALUES ($t,$ip,$name,$type,$detail);";
        cmd.Parameters.AddWithValue("$t", ev.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("$ip", ev.IpAddress);
        cmd.Parameters.AddWithValue("$name", (object?)ev.DeviceName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$type", ev.EventType);
        cmd.Parameters.AddWithValue("$detail", (object?)ev.Detail ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public List<MonitorEvent> ListMonitorEvents(int limit = 500)
    {
        var result = new List<MonitorEvent>();
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Id, Timestamp, IpAddress, DeviceName, EventType, Detail
                            FROM MonitorEvents ORDER BY Timestamp DESC LIMIT $lim;";
        cmd.Parameters.AddWithValue("$lim", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            result.Add(new MonitorEvent
            {
                Id = r.GetInt64(0),
                Timestamp = DateTime.Parse(r.GetString(1)),
                IpAddress = r.GetString(2),
                DeviceName = r.IsDBNull(3) ? "" : r.GetString(3),
                EventType = r.GetString(4),
                Detail = r.IsDBNull(5) ? "" : r.GetString(5),
            });
        }
        return result;
    }

    public void ClearMonitorEvents()
    {
        using var conn = Database.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM MonitorEvents;";
        cmd.ExecuteNonQuery();
    }
}
