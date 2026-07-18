using System.IO;

namespace ScanProfinet.Services;

/// <summary>
/// Centraliza os caminhos de dados do app. Fica em %LOCALAPPDATA%\ScanProfinet
/// para funcionar sem privilégio de administrador após a instalação.
/// </summary>
public static class AppPaths
{
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScanProfinet");

    public static string DatabaseFile => Path.Combine(DataFolder, "scanprofinet.db");
    public static string LogFolder => Path.Combine(DataFolder, "logs");

    static AppPaths()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(LogFolder);
    }
}
