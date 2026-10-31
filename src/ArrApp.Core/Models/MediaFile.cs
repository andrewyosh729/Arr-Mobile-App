namespace ArrApp.Core.Models;

/// <summary>A file on disk: Sonarr's <c>episodeFile</c> and Radarr's <c>movieFile</c> share this shape.</summary>
public sealed class MediaFile {
    public int Id { get; set; }
    public string? RelativePath { get; set; }
    public string? Path { get; set; }
    public long Size { get; set; }
    public DateTimeOffset? DateAdded { get; set; }
    public QualityModel? Quality { get; set; }
    public List<Language> Languages { get; set; } = [];
}
