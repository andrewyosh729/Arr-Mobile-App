using System.Text.Json.Serialization;

namespace ArrApp.Core.Models;

public sealed class SeasonStatistics {
    public int EpisodeFileCount { get; set; }
    public int EpisodeCount { get; set; }
    public int TotalEpisodeCount { get; set; }
    public long SizeOnDisk { get; set; }
    public double PercentOfEpisodes { get; set; }
}

public sealed class Season {
    public int SeasonNumber { get; set; }
    public bool Monitored { get; set; }
    public SeasonStatistics? Statistics { get; set; }
}

public sealed class SeriesStatistics {
    public int SeasonCount { get; set; }
    public int EpisodeFileCount { get; set; }
    public int EpisodeCount { get; set; }
    public int TotalEpisodeCount { get; set; }
    public long SizeOnDisk { get; set; }
    public double PercentOfEpisodes { get; set; }
}

/// <summary>A Sonarr series.</summary>
public sealed class Series : MediaItem {
    public string? Network { get; set; }
    public string? AirTime { get; set; }

    /// <summary>"standard", "daily" or "anime".</summary>
    public string? SeriesType { get; set; }

    public bool SeasonFolder { get; set; }
    public bool Ended { get; set; }
    public int TvdbId { get; set; }
    public DateTimeOffset? FirstAired { get; set; }
    public DateTimeOffset? NextAiring { get; set; }
    public DateTimeOffset? PreviousAiring { get; set; }

    public List<Season> Seasons { get; set; } = [];
    public SeriesStatistics? Statistics { get; set; }

    [JsonIgnore]
    public override long SizeOnDisk => Statistics?.SizeOnDisk ?? 0;
}

public sealed class Episode {
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public string Title { get; set; } = "";
    public string? Overview { get; set; }

    /// <summary>Calendar date, "yyyy-MM-dd". Absent for unaired specials.</summary>
    public DateOnly? AirDate { get; set; }

    public DateTimeOffset? AirDateUtc { get; set; }
    public int Runtime { get; set; }
    public bool HasFile { get; set; }
    public bool Monitored { get; set; }
    public int EpisodeFileId { get; set; }
    public MediaFile? EpisodeFile { get; set; }
}
