namespace ArrApp.Core.Models;

public sealed class Language {
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>One quality, e.g. "WEBDL-1080p".</summary>
public sealed class Quality {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Source { get; set; }
    public int Resolution { get; set; }
}

public sealed class QualityRevision {
    public int Version { get; set; }
    public bool IsRepack { get; set; }
}

/// <summary>A quality plus its revision, as attached to files and releases.</summary>
public sealed class QualityModel {
    public Quality Quality { get; set; } = new();
    public QualityRevision? Revision { get; set; }

    public string Name => Quality.Name;
}

/// <summary>A configured quality profile (the ones offered when adding an item).</summary>
public sealed class QualityProfile {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool UpgradeAllowed { get; set; }
}
