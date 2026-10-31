using Avalonia;
using Avalonia.Media;
using ArrApp.Core;

namespace ArrApp.UI.Theme;

/// <summary>Per-Arr accent colors, so the active context is obvious at a glance.</summary>
public static class AccentPalette {
    public static readonly Color Default = Color.Parse("#6C8CFF");
    public static readonly Color Sonarr = Color.Parse("#35C5F4");
    public static readonly Color Radarr = Color.Parse("#FFC230");

    public static Color For(ArrType? type) => type switch {
        ArrType.Sonarr => Sonarr,
        ArrType.Radarr => Radarr,
        _ => Default,
    };
}

public static class ThemeManager {
    /// <summary>Swaps the accent resources; everything bound with DynamicResource updates immediately.</summary>
    public static void ApplyAccent(Application app, ArrType? type) {
        var color = AccentPalette.For(type);
        app.Resources["AccentColor"] = color;
        app.Resources["AccentBrush"] = new SolidColorBrush(color);
    }
}
