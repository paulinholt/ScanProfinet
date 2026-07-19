using CommunityToolkit.Mvvm.ComponentModel;

namespace ScanProfinet.Models;

public enum ToastType { Info, Success, Warning, Danger }

/// <summary>Notificação exibida dentro da aplicação (canto superior, some sozinha).</summary>
public partial class ToastNotification : ObservableObject
{
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public ToastType Type { get; init; } = ToastType.Info;
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public string TimeText => Timestamp.ToString("HH:mm:ss");

    public string Glyph => Type switch
    {
        ToastType.Danger => "⛔",
        ToastType.Warning => "⚠",
        ToastType.Success => "✔",
        _ => "ℹ"
    };

    public string AccentHex => Type switch
    {
        ToastType.Danger => "#DC2626",
        ToastType.Warning => "#D97706",
        ToastType.Success => "#16A34A",
        _ => "#2563EB"
    };

    public string BgHex => Type switch
    {
        ToastType.Danger => "#FEF2F2",
        ToastType.Warning => "#FFFBEB",
        ToastType.Success => "#F0FDF4",
        _ => "#EFF6FF"
    };
}
