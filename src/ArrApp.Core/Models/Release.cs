using System.Text.Json.Serialization;

namespace ArrApp.Core.Models;

/// <summary>A search result from an indexer (interactive search). Same shape in both Arrs; season fields are Sonarr-only.</summary>
public sealed class Release {
    public string Guid { get; set; } = "";
    public string Title { get; set; } = "";
    public int IndexerId { get; set; }
    public string? Indexer { get; set; }
    public string? ReleaseGroup { get; set; }
    public long Size { get; set; }

    /// <summary>Age in days.</summary>
    public int Age { get; set; }

    public double AgeHours { get; set; }
    public DateTimeOffset? PublishDate { get; set; }
    public QualityModel? Quality { get; set; }
    public List<Language> Languages { get; set; } = [];

    /// <summary>"torrent" or "usenet".</summary>
    public string? Protocol { get; set; }

    public int? Seeders { get; set; }
    public int? Leechers { get; set; }
    public int CustomFormatScore { get; set; }

    public bool Approved { get; set; }
    public bool TemporarilyRejected { get; set; }
    public bool Rejected { get; set; }
    public bool DownloadAllowed { get; set; }
    public List<string> Rejections { get; set; } = [];

    public string? InfoUrl { get; set; }

    // Sonarr only
    public bool FullSeason { get; set; }
    public int? SeasonNumber { get; set; }

    [JsonIgnore]
    public bool IsTorrent => string.Equals(Protocol, "torrent", StringComparison.OrdinalIgnoreCase);
}
