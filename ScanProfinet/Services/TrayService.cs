using ScanProfinet.Models;

namespace ScanProfinet.Services;

/// <summary>
/// Ícone na bandeja do sistema (system tray) com menu e balões de notificação.
/// Usa System.Windows.Forms.NotifyIcon (qualificado por extenso para não conflitar com WPF).
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Text = "ScanProfinet — monitoramento de rede",
            Visible = true,
            Icon = LoadIcon()
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Abrir ScanProfinet", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitRequested?.Invoke());
        _icon.ContextMenuStrip = menu;
    }

    /// <summary>Exibe um balão de notificação na bandeja.</summary>
    public void ShowBalloon(string title, string message, ToastType type)
    {
        var icon = type switch
        {
            ToastType.Danger => System.Windows.Forms.ToolTipIcon.Error,
            ToastType.Warning => System.Windows.Forms.ToolTipIcon.Warning,
            _ => System.Windows.Forms.ToolTipIcon.Info
        };
        try { _icon.ShowBalloonTip(6000, title, message, icon); }
        catch (Exception ex) { AppLog.Error("Falha ao exibir balão na bandeja", ex); }
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/scanprofinet.ico");
            var info = System.Windows.Application.GetResourceStream(uri);
            if (info?.Stream != null)
                return new System.Drawing.Icon(info.Stream);
        }
        catch (Exception ex) { AppLog.Error("Falha ao carregar ícone da bandeja", ex); }
        return System.Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
