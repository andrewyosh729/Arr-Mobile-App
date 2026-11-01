using System;
using System.Threading;
using ArrApp.Core.Images;
using ArrApp.UI.Images;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace ArrApp.UI.Controls;

/// <summary>
/// A poster/fanart picture: a placeholder (with an optional letter) until the image arrives, then a short fade-in.
/// Pictures already in memory appear immediately, so scrolling back through a list doesn't flicker. Loading stops when
/// the control leaves the screen.
/// </summary>
public partial class ArtImage : UserControl {
    public static readonly StyledProperty<ImageRequest?> RequestProperty =
        AvaloniaProperty.Register<ArtImage, ImageRequest?>(nameof(Request));

    public static readonly StyledProperty<int> DecodeWidthProperty =
        AvaloniaProperty.Register<ArtImage, int>(nameof(DecodeWidth), 300);

    public static readonly StyledProperty<string?> FallbackTextProperty =
        AvaloniaProperty.Register<ArtImage, string?>(nameof(FallbackText));

    private CancellationTokenSource? _cts;

    public ArtImage() {
        InitializeComponent();
    }

    /// <summary>Where to get the picture from. Null shows only the placeholder.</summary>
    public ImageRequest? Request {
        get => GetValue(RequestProperty);
        set => SetValue(RequestProperty, value);
    }

    /// <summary>Pixel width to decode at (the display size times the screen density is plenty).</summary>
    public int DecodeWidth {
        get => GetValue(DecodeWidthProperty);
        set => SetValue(DecodeWidthProperty, value);
    }

    /// <summary>Shown in the placeholder, e.g. the title's first letter.</summary>
    public string? FallbackText {
        get => GetValue(FallbackTextProperty);
        set => SetValue(FallbackTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == FallbackTextProperty)
            Fallback.Text = FallbackText;
        else if (change.Property == CornerRadiusProperty)
            Frame.CornerRadius = CornerRadius;
        else if (change.Property == RequestProperty || change.Property == DecodeWidthProperty)
            Load();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        Load();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        _cts?.Cancel();
        base.OnDetachedFromVisualTree(e);
    }

    private async void Load() {
        _cts?.Cancel();
        var request = Request;
        Show(null, animate: false);

        if (request is null || TopLevel.GetTopLevel(this) is null)
            return; // nothing to load, or not on screen yet (OnAttachedToVisualTree loads then)
        if ((Application.Current as App)?.Services.GetService<BitmapService>() is not { } bitmaps)
            return;

        if (bitmaps.TryGetCached(request, DecodeWidth) is { } cached) {
            Show(cached, animate: false);
            return;
        }

        var cts = _cts = new CancellationTokenSource();
        try {
            var bitmap = await bitmaps.GetAsync(request, DecodeWidth, cts.Token);
            if (!cts.IsCancellationRequested)
                await Dispatcher.UIThread.InvokeAsync(() => Show(bitmap, animate: true));
        } catch (OperationCanceledException) {
            // scrolled away or the request changed
        }
    }

    private void Show(Bitmap? bitmap, bool animate) {
        var transitions = Picture.Transitions;
        if (!animate)
            Picture.Transitions = null; // jump straight to the end state, no fade

        Picture.Source = bitmap;
        Picture.Opacity = bitmap is null ? 0 : 1;

        if (!animate)
            Picture.Transitions = transitions;
    }
}
