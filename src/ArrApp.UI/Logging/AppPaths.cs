using System;
using System.IO;

namespace ArrApp.UI.Logging;

/// <summary>Per-user app storage. On Android this resolves to the app's private files directory.</summary>
public static class AppPaths {
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArrApp");

    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "logs");

    /// <summary>Downloaded artwork. Safe to delete at any time; it is simply fetched again.</summary>
    public static string ImageCacheDirectory { get; } = Path.Combine(DataDirectory, "cache", "images");
}
