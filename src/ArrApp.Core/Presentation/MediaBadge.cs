using ArrApp.Core.Models;

namespace ArrApp.Core.Presentation;

public enum BadgeKind {
    Neutral,
    Success,
    Warning,
    Danger,
}

/// <summary>
/// The small status chip on a media card. This is the Arr-specific part of an otherwise shared card:
/// series show episode progress ("4/12 eps"), movies show whether the file is there.
/// </summary>
public sealed record MediaBadge(string Text, BadgeKind Kind, double? Progress = null) {
    public static MediaBadge For(MediaItem item) => item switch {
        Series series => ForSeries(series),
        Movie movie => ForMovie(movie),
        _ => new MediaBadge("", BadgeKind.Neutral),
    };

    private static MediaBadge ForSeries(Series series) {
        var stats = series.Statistics;
        if (stats is null || stats.EpisodeCount <= 0)
            return new MediaBadge("No episodes", BadgeKind.Neutral);

        var files = stats.EpisodeFileCount;
        var total = stats.EpisodeCount;
        var text = $"{files}/{total} eps";
        var progress = Math.Clamp((double)files / total, 0, 1);

        var kind = files >= total ? BadgeKind.Success
            : files == 0 ? BadgeKind.Danger
            : BadgeKind.Warning;
        return new MediaBadge(text, kind, progress);
    }

    private static MediaBadge ForMovie(Movie movie) {
        if (movie.HasFile)
            return new MediaBadge("Downloaded", BadgeKind.Success);
        if (!movie.IsAvailable)
            return new MediaBadge("Unreleased", BadgeKind.Neutral);
        if (!movie.Monitored)
            return new MediaBadge("Unmonitored", BadgeKind.Neutral);
        return new MediaBadge("Missing", BadgeKind.Danger);
    }

    /// <summary>Human wording for the Arr's raw status values ("inCinemas" becomes "In cinemas").</summary>
    public static string StatusLabel(string? status) => status switch {
        null or "" => "",
        "continuing" => "Continuing",
        "ended" => "Ended",
        "upcoming" => "Upcoming",
        "released" => "Released",
        "inCinemas" => "In cinemas",
        "announced" => "Announced",
        "deleted" => "Deleted",
        _ => char.ToUpperInvariant(status[0]) + status[1..],
    };
}
