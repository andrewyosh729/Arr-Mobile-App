using System.Text.Json.Serialization;

namespace ArrApp.Core.Models;

/// <summary>A Radarr movie.</summary>
public sealed class Movie : MediaItem {
    public string? Studio { get; set; }
    public string? OriginalTitle { get; set; }
    public bool HasFile { get; set; }
    public bool IsAvailable { get; set; }

    /// <summary>"announced", "inCinemas" or "released": when Radarr starts looking for it.</summary>
    public string? MinimumAvailability { get; set; }

    public DateTimeOffset? InCinemas { get; set; }
    public DateTimeOffset? PhysicalRelease { get; set; }
    public DateTimeOffset? DigitalRelease { get; set; }
    public MediaFile? MovieFile { get; set; }

    [JsonPropertyName("sizeOnDisk")]
    public long SizeOnDiskBytes { get; set; }

    [JsonIgnore]
    public override long SizeOnDisk => SizeOnDiskBytes;
}
