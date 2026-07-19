using System.IO;

namespace ScanProfinet.Services;

/// <summary>
/// Log simples em arquivo (um arquivo por dia). Thread-safe.
/// Usado para diagnóstico geral e para o registro de eventos de rede.
/// </summary>
public static class AppLog
{
    private static readonly object _lock = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERRO", ex == null ? message : $"{message} :: {ex.GetType().Name}: {ex.Message}");

    /// <summary>Log DEDICADO de reendereçamentos (Set IP / Set Device Name). Arquivo mensal.</summary>
    public static void Readdress(string message) => WriteCategory("reenderecamentos", message);

    /// <summary>Log DEDICADO de eventos de monitoramento (quedas/retornos/adições/oscilações). Arquivo mensal.</summary>
    public static void MonitorLog(string message) => WriteCategory("monitoramento", message);

    private static void Write(string level, string message)
        => Append($"scanprofinet-{DateTime.Now:yyyy-MM-dd}.log",
                  $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}");

    private static void WriteCategory(string category, string message)
        => Append($"{category}-{DateTime.Now:yyyy-MM}.log",
                  $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}");

    private static void Append(string fileName, string line)
    {
        try
        {
            var file = Path.Combine(AppPaths.LogFolder, fileName);
            lock (_lock)
                File.AppendAllText(file, line + Environment.NewLine);
        }
        catch { /* nunca deixar o log derrubar a aplicação */ }
    }
}
