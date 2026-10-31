using ArrApp.Core;

namespace ArrApp.UI;

/// <summary>
/// Implementations only a platform head can provide. Each head's entry point must set these
/// before the Avalonia app starts (Desktop: <c>Program.Main</c>, Android: <c>AndroidApp</c>, iOS: <c>Application.Main</c>).
/// </summary>
public static class PlatformServices {
    public static IApiKeyProtector? ApiKeyProtector { get; set; }
}
