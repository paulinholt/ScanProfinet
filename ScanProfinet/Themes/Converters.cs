using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ScanProfinet.Models;

namespace ScanProfinet.Themes;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value != null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Cor de fundo para o status de comparação.</summary>
public class CompareStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var color = value is CompareStatus s ? s switch
        {
            CompareStatus.Removed => "#FDE8E8",
            CompareStatus.Added => "#FEF3C7",
            CompareStatus.Changed => "#E0EAFF",
            _ => "#FFFFFF"
        } : "#FFFFFF";
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Cor forte (texto/etiqueta) para o status de comparação.</summary>
public class CompareStatusToAccentConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var color = value is CompareStatus s ? s switch
        {
            CompareStatus.Removed => "#DC2626",
            CompareStatus.Added => "#B45309",
            CompareStatus.Changed => "#2563EB",
            _ => "#16A34A"
        } : "#64748B";
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Cor do "pill" de estado do monitor.</summary>
public class MonitorStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var color = value is MonitorState s ? s switch
        {
            MonitorState.Online => "#16A34A",
            MonitorState.Offline => "#DC2626",
            MonitorState.Unstable => "#D97706",
            _ => "#94A3B8"
        } : "#94A3B8";
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public class EventTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var color = (value as string) switch
        {
            "QUEDA" => "#DC2626",
            "OSCILANDO" => "#D97706",
            "RETORNO" => "#16A34A",
            _ => "#64748B"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Converte latência (ms) em altura de barra (0–36px). -1 = sem resposta (altura cheia).</summary>
public class LatencyToHeightConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not double v) return 2.0;
        if (v < 0) return 36.0;                 // sem resposta → barra cheia
        double clamped = Math.Min(v, 100);      // teto de 100ms para escala
        return 2.0 + clamped / 100.0 * 34.0;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Ícone (Geometry) por tipo de dispositivo, aproximando os ícones do PRONETA.</summary>
public class NodeKindToGeometryConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        string data = value is NodeKind k ? k switch
        {
            NodeKind.Controller => "M4,5 H18 V17 H4 Z M7,5 V2 M11,5 V2 M15,5 V2 M7,17 V20 M11,17 V20 M15,17 V20 M2,8 H4 M2,11 H4 M2,14 H4 M18,8 H20 M18,11 H20 M18,14 H20 M9,9 H13 V13 H9 Z",
            NodeKind.Switch => "M2,7 H20 V15 H2 Z M5,9 H7 V13 H5 Z M9,9 H11 V13 H9 Z M13,9 H15 V13 H13 Z M17,9 H19 V13 H17 Z",
            NodeKind.IODevice => "M5,2 H17 V20 H5 Z M8,6 H14 M8,9 H14 M8,12 H14 M8,15 H14",
            _ => "M4,4 H18 V18 H4 Z M8,8 H14 V14 H8 Z"
        } : "M4,4 H18 V18 H4 Z";
        return Geometry.Parse(data);
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Cor do ícone por tipo de dispositivo.</summary>
public class NodeKindToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var hex = value is NodeKind k ? k switch
        {
            NodeKind.Controller => "#2563EB",
            NodeKind.Switch => "#7C3AED",
            NodeKind.IODevice => "#0891B2",
            _ => "#94A3B8"
        } : "#94A3B8";
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Converte uma string hexadecimal (#RRGGBB) em SolidColorBrush.</summary>
public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString((string)value)); }
        catch { return new SolidColorBrush(Colors.Gray); }
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Cor da barra de latência: verde (baixa) → âmbar (média) → vermelho (sem resposta).</summary>
public class LatencyToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not double v) return new SolidColorBrush(Colors.Gray);
        string hex = v < 0 ? "#DC2626" : v <= 20 ? "#16A34A" : v <= 60 ? "#D97706" : "#EA580C";
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
