using Microsoft.Data.Sqlite;
using ScanProfinet.Services;

namespace ScanProfinet.Data;

/// <summary>
/// Banco local SQLite (arquivo único, sem servidor, sem licença).
/// Cria o esquema automaticamente na primeira execução.
/// </summary>
public static class Database
{
    public static string ConnectionString { get; } =
        new SqliteConnectionStringBuilder { DataSource = AppPaths.DatabaseFile }.ToString();

    public static SqliteConnection Open()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return conn;
    }

    public static void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Snapshots (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    Name      TEXT NOT NULL,
    Notes     TEXT,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS SnapshotDevices (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    SnapshotId  INTEGER NOT NULL,
    MacAddress  TEXT NOT NULL,
    IpAddress   TEXT,
    SubnetMask  TEXT,
    Gateway     TEXT,
    DeviceName  TEXT,
    DeviceVendor TEXT,
    DeviceRole  TEXT,
    VendorId    INTEGER,
    DeviceId    INTEGER,
    FOREIGN KEY (SnapshotId) REFERENCES Snapshots(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS MonitorEvents (
    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp  TEXT NOT NULL,
    IpAddress  TEXT NOT NULL,
    DeviceName TEXT,
    EventType  TEXT NOT NULL,
    Detail     TEXT
);

CREATE INDEX IF NOT EXISTS IX_SnapshotDevices_Snapshot ON SnapshotDevices(SnapshotId);
CREATE INDEX IF NOT EXISTS IX_MonitorEvents_Time ON MonitorEvents(Timestamp);
";
        cmd.ExecuteNonQuery();
        AppLog.Info($"Banco inicializado em {AppPaths.DatabaseFile}");
    }
}
