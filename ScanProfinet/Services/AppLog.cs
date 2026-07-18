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

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
            var file = Path.Combine(AppPaths.LogFolder, $"scanprofinet-{DateTime.Now:yyyy-MM-dd}.log");
            lock (_lock)
                File.AppendAllText(file, line);
        }
        catch { /* nunca deixar o log derrubar a aplicação */ }
    }
}
