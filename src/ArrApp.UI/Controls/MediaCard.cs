using Avalonia;
using Avalonia.Controls;

namespace ArrApp.UI.Controls;

public enum MediaCardLayout {
    /// <summary>Poster with overlays and the title underneath: for the grid.</summary>
    Grid,

    /// <summary>A row: small poster, title, details and badge: for the list.</summary>
    List,
}

/// <summary>
/// One card for a series or a movie. Set <see cref="Layout"/> and put a <c>MediaCardViewModel</c> in the DataContext;
/// the same view model drives both layouts.
/// </summary>
public sealed class MediaCard : UserControl {
    public static readonly StyledProperty<MediaCardLayout> LayoutProperty =
        AvaloniaProperty.Register<MediaCard, MediaCardLayout>(nameof(Layout));

    public MediaCard() {
        Content = Build();
    }

    public MediaCardLayout Layout {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == LayoutProperty)
            Content = Build();
    }

    // The inner view inherits this control's DataContext, so no wiring is needed.
    private Control Build() => Layout == MediaCardLayout.List ? new MediaCardListView() : new MediaCardGridView();
}
