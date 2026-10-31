namespace ArrApp.Core.Models;

public sealed class QueueStatusMessage {
    public string? Title { get; set; }
    public List<string> Messages { get; set; } = [];
}

/// <summary>One download in the Arr's queue. <c>EpisodeId</c>/<c>SeriesId</c> are set by Sonarr, <c>MovieId</c> by Radarr.</summary>
public sealed class QueueItem {
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public long Size { get; set; }

    /// <summary>Bytes still to download.</summary>
    public double SizeLeft { get; set; }

    /// <summary>"downloading", "paused", "queued", "completed", "failed", "warning", ...</summary>
    public string? Status { get; set; }

    /// <summary>"ok", "warning" or "error".</summary>
    public string? TrackedDownloadStatus { get; set; }

    public string? TrackedDownloadState { get; set; }
    public string? ErrorMessage { get; set; }
    public List<QueueStatusMessage> StatusMessages { get; set; } = [];
    public string? Protocol { get; set; }
    public string? DownloadClient { get; set; }
    public string? Indexer { get; set; }
    public DateTimeOffset? Added { get; set; }
    public DateTimeOffset? EstimatedCompletionTime { get; set; }
    public string? TimeLeft { get; set; }
    public QualityModel? Quality { get; set; }

    public int? SeriesId { get; set; }
    public int? EpisodeId { get; set; }
    public int? SeasonNumber { get; set; }
    public int? MovieId { get; set; }

    /// <summary>0 to 100.</summary>
    public double ProgressPercent => Size <= 0 ? 0 : Math.Clamp((Size - SizeLeft) / Size * 100, 0, 100);
}

/// <summary>The Arr's paged wrapper around the queue (and other paged lists).</summary>
public sealed class QueuePage {
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public List<QueueItem> Records { get; set; } = [];
}
