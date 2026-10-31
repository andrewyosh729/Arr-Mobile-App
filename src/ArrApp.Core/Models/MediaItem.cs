using System.Text.Json.Serialization;

namespace ArrApp.Core.Models;

public sealed class MediaImage {
    /// <summary>"poster", "fanart", "banner", "clearlogo", "screenshot", ...</summary>
    public string CoverType { get; set; } = "";

    /// <summary>Path on the Arr server (served via its <c>MediaCover</c> endpoint, needs the API key).</summary>
    public string? Url { get; set; }

    /// <summary>Public image URL (TheTVDB / TMDB), usable without the server.</summary>
    public string? RemoteUrl { get; set; }
}

/// <summary>
/// What a library entry has in common across Arrs, so cards, lists, lookup and the add sheet are written once.
/// <see cref="Series"/> and <see cref="Movie"/> add the Arr-specific parts.
/// </summary>
public abstract class MediaItem {
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? SortTitle { get; set; }
    public string? Overview { get; set; }
    public int Year { get; set; }

    /// <summary>Arr-reported state, e.g. "continuing"/"ended" (series) or "released"/"announced" (movies).</summary>
    public string? Status { get; set; }

    public bool Monitored { get; set; }
    public int QualityProfileId { get; set; }
    public string? Path { get; set; }
    public string? RootFolderPath { get; set; }
    public int Runtime { get; set; }
    public string? Certification { get; set; }
    public int TmdbId { get; set; }
    public string? ImdbId { get; set; }
    public string? TitleSlug { get; set; }
    public DateTimeOffset? Added { get; set; }

    public List<MediaImage> Images { get; set; } = [];
    public List<string> Genres { get; set; } = [];
    public List<int> Tags { get; set; } = [];

    /// <summary>Total size of this item's files. Lives at a different place in each Arr's JSON, hence abstract.</summary>
    [JsonIgnore]
    public abstract long SizeOnDisk { get; }

    /// <summary>True once the item is in the library (lookup results are not).</summary>
    [JsonIgnore]
    public bool IsInLibrary => Id > 0;

    [JsonIgnore]
    public MediaImage? Poster => Image("poster");

    [JsonIgnore]
    public MediaImage? Fanart => Image("fanart");

    public MediaImage? Image(string coverType)
        => Images.FirstOrDefault(i => string.Equals(i.CoverType, coverType, StringComparison.OrdinalIgnoreCase));
}
