using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.Core.Presentation;
using ArrApp.UI.Errors;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

/// <summary>
/// The library of the active server (series for Sonarr, movies for Radarr) as a grid or list of cards.
/// It loads when a server becomes active, reloads when the active server changes (a result that arrives for a
/// server the user has already left is dropped), and remembers the grid/list choice.
/// </summary>
public sealed partial class MediaCollectionViewModel : ViewModelBase {
    public const string GridLayoutSettingKey = "LibraryLayoutIsGrid";

    private readonly ArrContext _context;
    private readonly IArrClientFactory _clients;
    private readonly ISettingsStore _settings;
    private readonly IToastService _toasts;

    private CancellationTokenSource? _loadCts;
    private Task _loadTask = Task.CompletedTask;
    private bool _layoutLoaded;
    private IReadOnlyList<MediaCardViewModel> _all = [];
    private MediaQuery _query = new();

    public MediaCollectionViewModel(ArrContext context, IArrClientFactory clients, ISettingsStore settings, IToastService toasts) {
        _context = context;
        _clients = clients;
        _settings = settings;
        _toasts = toasts;

        context.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(ArrContext.Active))
                _loadTask = LoadAsync(isRefresh: false);
            else if (e.PropertyName == nameof(ArrContext.MediaNoun))
                OnPropertyChanged(nameof(Title));
        };
    }

    /// <summary>What is on screen: the library after the current search, filter and sort.</summary>
    public ObservableCollection<MediaCardViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsList))]
    private bool _isGrid = true;

    /// <summary>True during the first load of a server's library, before there is anything to show.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowContent))]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    /// <summary>Set when the first load fails; shown with a retry button. A failed refresh keeps the old items instead.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(ShowContent))]
    private string? _errorMessage;

    /// <summary>Loaded fine, but the server has nothing in its library.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowContent))]
    private bool _isEmpty;

    /// <summary>The library has items, but the current search/filter keeps none of them.</summary>
    [ObservableProperty]
    private bool _noMatches;

    public bool IsList => !IsGrid;

    /// <summary>Label for the layout button: names the layout it switches to.</summary>
    public string LayoutToggleText => IsGrid ? "☰  List" : "▦  Grid";

    /// <summary>
    /// What the screen renders: <see cref="Items"/> grouped into rows (one card per row in the list layout, <see cref="Columns"/>
    /// per row in the grid). A new list each time, so the UI gets one change notification instead of one per row.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<MediaRowViewModel> _rows = [];

    /// <summary>How many cards fit across in the grid. The view reports this from its width.</summary>
    public int Columns { get; private set; } = 3;

    public void SetColumns(int columns) {
        columns = Math.Max(1, columns);
        if (columns == Columns)
            return;

        Columns = columns;
        OnPropertyChanged(nameof(Columns));
        RebuildRows();
    }
    public bool HasError => ErrorMessage is not null;
    public bool ShowContent => !IsLoading && !HasError && !IsEmpty;

    /// <summary>"Series" or "Movies", following the active server.</summary>
    public string Title => _context.MediaNoun is { Length: > 0 } noun ? noun : "Library";

    /// <summary>How many items the library has, before any search or filter.</summary>
    public int TotalCount => _all.Count;

    public MediaQuery Query {
        get => _query;
        set {
            _query = value;
            ApplyQuery();
        }
    }

    /// <summary>Completes when the load in progress (if any) has finished. For tests and callers that need to wait.</summary>
    public Task WhenIdleAsync() => _loadTask;

    partial void OnIsGridChanged(bool value) {
        OnPropertyChanged(nameof(LayoutToggleText));
        RebuildRows();
        if (_layoutLoaded)
            _ = SaveLayoutAsync(value);
    }

    [RelayCommand]
    private void ToggleLayout() => IsGrid = !IsGrid;

    [RelayCommand]
    private Task RefreshAsync() => _loadTask = LoadAsync(isRefresh: true);

    [RelayCommand]
    private Task RetryAsync() => _loadTask = LoadAsync(isRefresh: false);

    private async Task LoadAsync(bool isRefresh) {
        _loadCts?.Cancel();
        var cts = _loadCts = new CancellationTokenSource();

        var server = _context.Active;
        if (server is null) {
            Reset();
            IsLoading = false;
            IsRefreshing = false;
            return;
        }

        await RestoreLayoutAsync();

        if (isRefresh) {
            IsRefreshing = true;
        } else {
            // Switching server or retrying: don't show the previous server's items while the new ones load.
            IsLoading = true; // before Reset, so "library is empty" is not reported while it is still loading
            Reset();
        }

        try {
            var library = await _clients.Create(server).GetLibraryAsync(cts.Token);
            if (cts.IsCancellationRequested)
                return; // the active server changed while this was loading

            _all = library.Select(item => new MediaCardViewModel(item, server)).ToList();
            ErrorMessage = null;
            IsLoading = false;
            ApplyQuery();
        } catch (OperationCanceledException) {
            // superseded by a newer load
        } catch (ArrApiException ex) when (!cts.IsCancellationRequested) {
            if (isRefresh && _all.Count > 0)
                _toasts.Show(ex.Message, ToastKind.Error); // keep what is on screen; the pull just didn't work
            else
                ErrorMessage = ex.Message;
        } catch (Exception ex) when (!cts.IsCancellationRequested) {
            ErrorMessage = "Couldn't load the library. " + ex.Message;
        } finally {
            if (ReferenceEquals(_loadCts, cts)) {
                IsLoading = false;
                IsRefreshing = false;
                UpdateEmptyStates();
            }
        }
    }

    private void Reset() {
        _all = [];
        ErrorMessage = null;
        Items.Clear();
        RebuildRows();
        OnPropertyChanged(nameof(TotalCount));
        UpdateEmptyStates();
    }

    private void ApplyQuery() {
        var byId = _all.ToDictionary(card => card.Id);
        var ordered = MediaQueryEngine.Apply(_all.Select(card => card.Item), _query).Select(item => byId[item.Id]);

        Items.Clear();
        foreach (var card in ordered)
            Items.Add(card);

        RebuildRows();
        OnPropertyChanged(nameof(TotalCount));
        UpdateEmptyStates();
    }

    private void UpdateEmptyStates() {
        var loaded = !IsLoading && ErrorMessage is null && _context.Active is not null;
        IsEmpty = loaded && _all.Count == 0;
        NoMatches = loaded && _all.Count > 0 && Items.Count == 0;
    }

    private async Task RestoreLayoutAsync() {
        if (_layoutLoaded)
            return;

        try {
            IsGrid = await _settings.GetAsync(GridLayoutSettingKey) != "0";
        } catch (Exception) {
            // a settings problem just means the default layout
        }

        _layoutLoaded = true;
    }

    private async Task SaveLayoutAsync(bool isGrid) {
        try {
            await _settings.SetAsync(GridLayoutSettingKey, isGrid ? "1" : "0");
        } catch (Exception) {
            // not worth interrupting the user for
        }
    }

    private void RebuildRows() {
        var rows = new List<MediaRowViewModel>();

        if (IsGrid) {
            for (var i = 0; i < Items.Count; i += Columns) {
                var cells = Enumerable.Range(i, Columns)
                    .Select(index => new MediaCellViewModel(index < Items.Count ? Items[index] : null))
                    .ToList();
                rows.Add(new GridRowViewModel(cells));
            }
        } else {
            rows.AddRange(Items.Select(card => new ListRowViewModel(card)));
        }

        Rows = rows;
    }
}
