namespace ArrApp.Core.Presentation;

/// <summary>Words for the sort and filter controls, so the UI says "Newest first" instead of a bare arrow.</summary>
public static class MediaSortText {
    public static string Name(MediaSortKey key) => key switch {
        MediaSortKey.Title => "Title",
        MediaSortKey.DateAdded => "Date added",
        MediaSortKey.Year => "Year",
        MediaSortKey.Size => "Size on disk",
        _ => key.ToString(),
    };

    public static string Direction(MediaSortKey key, bool descending) => key switch {
        MediaSortKey.Title => descending ? "Z to A" : "A to Z",
        MediaSortKey.DateAdded => descending ? "Newest first" : "Oldest first",
        MediaSortKey.Year => descending ? "Latest first" : "Earliest first",
        MediaSortKey.Size => descending ? "Largest first" : "Smallest first",
        _ => descending ? "Descending" : "Ascending",
    };

    public static string Filter(MediaFilter filter) => filter switch {
        MediaFilter.All => "All",
        MediaFilter.Monitored => "Monitored",
        MediaFilter.Unmonitored => "Unmonitored",
        MediaFilter.Missing => "Missing",
        MediaFilter.Downloaded => "Downloaded",
        _ => filter.ToString(),
    };
}
