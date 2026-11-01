using System.Globalization;
using ArrApp.Core.Models;

namespace ArrApp.Core.Presentation;

public enum MediaSortKey {
    Title,
    DateAdded,
    Year,
    Size,
}

public enum MediaFilter {
    All,
    Monitored,
    Unmonitored,

    /// <summary>Something is still to download: a movie that is out but has no file, or a series with episodes missing.</summary>
    Missing,

    /// <summary>Everything that has aired is downloaded.</summary>
    Downloaded,
}

/// <summary>What the user asked of a library list: what to search for, how to order it and what to keep.</summary>
public sealed record MediaQuery(
    string Search = "",
    MediaSortKey SortKey = MediaSortKey.Title,
    bool Descending = false,
    MediaFilter Filter = MediaFilter.All) {
    /// <summary>The natural direction for a sort: A to Z, but newest, latest and biggest first.</summary>
    public static bool DefaultDescending(MediaSortKey key) => key != MediaSortKey.Title;

    /// <summary>A query sorted by <paramref name="key"/> in its natural direction.</summary>
    public MediaQuery SortedBy(MediaSortKey key) => this with { SortKey = key, Descending = DefaultDescending(key) };
}

/// <summary>What a series or movie is, in the terms the filters use. Matches the status badge on the card.</summary>
public static class MediaState {
    public static bool IsDownloaded(MediaItem item) => item switch {
        Movie movie => movie.HasFile,
        Series series => series.Statistics is { EpisodeCount: > 0 } s && s.EpisodeFileCount >= s.EpisodeCount,
        _ => false,
    };

    public static bool HasMissing(MediaItem item) => item switch {
        // Out, wanted, and no file yet. A monitored movie that isn't released can't be "missing".
        Movie movie => !movie.HasFile && movie.IsAvailable && movie.Monitored,
        Series series => series.Statistics is { EpisodeCount: > 0 } s && s.EpisodeFileCount < s.EpisodeCount,
        _ => false,
    };
}

public static class MediaQueryEngine {
    /// <summary>Searches, filters and sorts. Never changes the input; the same query always gives the same order.</summary>
    public static IReadOnlyList<MediaItem> Apply(IEnumerable<MediaItem> items, MediaQuery query) {
        var tokens = Tokens(query.Search);

        var matches = items.Where(item => Matches(item, query.Filter) && MatchesSearch(item, tokens));
        return Sort(matches, query);
    }

    /// <summary>How many items each filter would keep, for the counts on the filter chips.</summary>
    public static int Count(IEnumerable<MediaItem> items, MediaFilter filter) => items.Count(item => Matches(item, filter));

    public static bool Matches(MediaItem item, MediaFilter filter) => filter switch {
        MediaFilter.All => true,
        MediaFilter.Monitored => item.Monitored,
        MediaFilter.Unmonitored => !item.Monitored,
        MediaFilter.Missing => MediaState.HasMissing(item),
        MediaFilter.Downloaded => MediaState.IsDownloaded(item),
        _ => true,
    };

    // ---- Search ----

    // Every word must appear in the title or the year, in any order: "dune 2024" and "2024 dune" both find the film.
    private static string[] Tokens(string search)
        => search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool MatchesSearch(MediaItem item, string[] tokens)
        => tokens.All(token => Contains(item.Title, token) || (item.Year > 0 && item.Year.ToString().Contains(token, StringComparison.Ordinal)));

    // Ignoring case and accents, so "ecoute" finds "Écoute".
    private static bool Contains(string text, string token)
        => CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, token, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    // ---- Sort ----

    private static IReadOnlyList<MediaItem> Sort(IEnumerable<MediaItem> items, MediaQuery query) {
        var list = items.ToList();
        var direction = query.Descending ? -1 : 1;

        list.Sort((a, b) => {
            // Items without a value (no year, never added) always go last, whichever way the list is sorted.
            var (hasA, hasB) = (HasValue(a, query.SortKey), HasValue(b, query.SortKey));
            if (hasA != hasB)
                return hasA ? -1 : 1;

            var primary = hasA ? Compare(a, b, query.SortKey) * direction : 0;
            return primary != 0 ? primary : CompareTitles(a, b);
        });

        return list;
    }

    private static bool HasValue(MediaItem item, MediaSortKey key) => key switch {
        MediaSortKey.Year => item.Year > 0,
        MediaSortKey.DateAdded => item.Added is not null,
        _ => true,
    };

    private static int Compare(MediaItem a, MediaItem b, MediaSortKey key) => key switch {
        MediaSortKey.Title => CompareTitles(a, b),
        MediaSortKey.DateAdded => Nullable.Compare(a.Added, b.Added),
        MediaSortKey.Year => a.Year.CompareTo(b.Year),
        MediaSortKey.Size => a.SizeOnDisk.CompareTo(b.SizeOnDisk),
        _ => 0,
    };

    // The Arr's own sort title ("severance", without "The") when it has one, so "The Wire" files under W.
    private static int CompareTitles(MediaItem a, MediaItem b) => string.Compare(
        SortTitle(a), SortTitle(b), CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);

    private static string SortTitle(MediaItem item) => string.IsNullOrWhiteSpace(item.SortTitle) ? item.Title : item.SortTitle;
}
