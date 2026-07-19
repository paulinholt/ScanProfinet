using System.Collections.ObjectModel;
using System.Windows.Threading;
using ScanProfinet.Models;

namespace ScanProfinet.Services;

/// <summary>
/// Central de notificações: exibe toasts dentro da aplicação e (opcionalmente)
/// balões na bandeja do sistema.
/// </summary>
public class NotificationService
{
    private readonly Action<Action> _ui;

    /// <summary>Toasts ativos exibidos no canto da janela.</summary>
    public ObservableCollection<ToastNotification> Toasts { get; } = new();

    /// <summary>Bandeja (definida pela MainWindow quando o ícone é criado).</summary>
    public TrayService? Tray { get; set; }

    public NotificationService(Action<Action> ui) => _ui = ui;

    public void Notify(string title, string message, ToastType type, bool tray = true)
    {
        _ui(() =>
        {
            var toast = new ToastNotification { Title = title, Message = message, Type = type };
            Toasts.Insert(0, toast);
            while (Toasts.Count > 5) Toasts.RemoveAt(Toasts.Count - 1);

            // Some sozinho após 8s.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            timer.Tick += (_, _) => { timer.Stop(); Toasts.Remove(toast); };
            timer.Start();

            if (tray) Tray?.ShowBalloon(title, message, type);
        });
    }

    public void Dismiss(ToastNotification toast) => _ui(() => Toasts.Remove(toast));
}
