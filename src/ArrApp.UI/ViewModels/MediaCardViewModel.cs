using System.Linq;
using ArrApp.Core;
using ArrApp.Core.Images;
using ArrApp.Core.Models;
using ArrApp.Core.Presentation;

namespace ArrApp.UI.ViewModels;

/// <summary>Everything a media card shows, for a series or a movie. Built once per item, so it is plain and immutable.</summary>
public sealed class MediaCardViewModel : ViewModelBase {
    public MediaCardViewModel(MediaItem item, ServerConnection server) {
        Item = item;
        Badge = MediaBadge.For(item);

        var poster = item.Poster;
        PosterRequest = poster is null ? new ImageRequest(null, null, null, null) : ImageRequest.For(server, poster);
    }

    public MediaItem Item { get; }
    public int Id => Item.Id;
    public string Title => Item.Title;

    /// <summary>"2022", or empty when the Arr doesn't know the year yet.</summary>
    public string YearText => Item.Year > 0 ? Item.Year.ToString() : "";

    /// <summary>Second line in list rows: "2022 · Continuing".</summary>
    public string Detail {
        get {
            var parts = new[] { YearText, MediaBadge.StatusLabel(Item.Status) }.Where(p => p.Length > 0);
            return string.Join(" · ", parts);
        }
    }

    /// <summary>Shown in the poster placeholder.</summary>
    public string Initial => Title.Length > 0 ? char.ToUpperInvariant(Title[0]).ToString() : "?";

    public ImageRequest PosterRequest { get; }
    public bool IsMonitored => Item.Monitored;

    public MediaBadge Badge { get; }
    public string BadgeText => Badge.Text;
    public bool HasBadge => Badge.Text.Length > 0;
    public bool BadgeIsSuccess => Badge.Kind == BadgeKind.Success;
    public bool BadgeIsWarning => Badge.Kind == BadgeKind.Warning;
    public bool BadgeIsDanger => Badge.Kind == BadgeKind.Danger;

    public bool HasProgress => Badge.Progress is not null;
    public double Progress => Badge.Progress ?? 0;
}
