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
using ArrApp.UI.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

/// <summary>One filter button ("Missing 12"). The count is for the whole library, so it doesn't jump around while typing a search.</summary>
public sealed partial class FilterChipViewModel(MediaFilter filter) : ObservableObject {
    public MediaFilter Filter { get; } = filter;
    public string Label => MediaSortText.Filter(Filter);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text))]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;

    public string Text => $"{Label} {Count}";
}

/// <summary>
/// The library of the active server (series for Sonarr, movies for Radarr) as a grid or list of cards, with search,
/// sort and filter. It loads when a server becomes active, reloads when the active server changes (a result that
/// arrives for a server the user has already left is dropped), and remembers the grid/list choice.
/// </summary>
public sealed partial class MediaCollectionViewModel : ViewModelBase {
    public const string GridLayoutSettingKey = "LibraryLayoutIsGrid";
    public static readonly TimeSpan DefaultSearchDelay = TimeSpan.FromMilliseconds(250);

    private readonly ArrContext _context;
    private readonly IArrClientFactory _clients;
    private readonly ISettingsStore _settings;
    private readonly IToastService _toasts;
    private readonly IBottomSheetService _sheets;
    private readonly TimeSpan _searchDelay;

    private CancellationTokenSource? _loadCts;
    private Task _loadTask = Task.CompletedTask;
    private CancellationTokenSource? _searchCts;
    private Task _searchTask = Task.CompletedTask;
    private bool _syncingSearchText;
    private bool _layoutLoaded;
    private IReadOnlyList<MediaCardViewModel> _all = [];
    private MediaQuery _query = new();

    public MediaCollectionViewModel(ArrContext context, IArrClientFactory clients, ISettingsStore settings, IToastService toasts,
        IBottomSheetService sheets, TimeSpan? searchDelay = null) {
        _context = context;
        _clients = clients;
        _settings = settings;
        _toasts = toasts;
        _sheets = sheets;
        _searchDelay = searchDelay ?? DefaultSearchDelay;

        FilterChips = Enum.GetValues<MediaFilter>().Select(f => new FilterChipViewModel(f)).ToList();
        UpdateQueryState();

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

    /// <summary>What is typed in the search box. It is applied to the list a moment after typing stops.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    private string _searchText = "";

    /// <summary>What the screen renders: <see cref="Items"/> grouped into rows. A new list each time: one change notification.</summary>
    [ObservableProperty]
    private IReadOnlyList<MediaRowViewModel> _rows = [];

    public bool IsList => !IsGrid;
    public bool HasSearchText => SearchText.Length > 0;
    public bool HasError => ErrorMessage is not null;
    public bool ShowContent => !IsLoading && !HasError && !IsEmpty;

    /// <summary>"Series" or "Movies", following the active server.</summary>
    public string Title => _context.MediaNoun is { Length: > 0 } noun ? noun : "Library";

    /// <summary>How many items the library has, before any search or filter.</summary>
    public int TotalCount => _all.Count;

    /// <summary>True once there is a library to search, sort and filter.</summary>
    public bool HasItems => _all.Count > 0;

    public string SearchPlaceholder => $"Search {TotalCount} {Title.ToLowerInvariant()}";

    /// <summary>Label for the layout button: names the layout it switches to.</summary>
    public string LayoutToggleText => IsGrid ? "☰  List" : "▦  Grid";

    /// <summary>"Title · A to Z": what the list is sorted by right now.</summary>
    public string SortChipText => $"{MediaSortText.Name(_query.SortKey)} · {MediaSortText.Direction(_query.SortKey, _query.Descending)}";

    public IReadOnlyList<FilterChipViewModel> FilterChips { get; }

    /// <summary>True when a search or filter is hiding part of the library.</summary>
    public bool IsNarrowed => _query.Search.Length > 0 || _query.Filter != MediaFilter.All;

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

    /// <summary>The applied search, sort and filter. Setting it applies it at once (the search box lags behind by a moment).</summary>
    public MediaQuery Query {
        get => _query;
        set => ApplyQueryChange(value, syncSearchText: true);
    }

    /// <summary>Completes when the load in progress (if any) has finished. For tests and callers that need to wait.</summary>
    public Task WhenIdleAsync() => _loadTask;

    /// <summary>Completes when the typed search has been applied to the list.</summary>
    public Task WhenSearchAppliedAsync() => _searchTask;

    partial void OnIsGridChanged(bool value) {
        OnPropertyChanged(nameof(LayoutToggleText));
        RebuildRows();
        if (_layoutLoaded)
            _ = SaveLayoutAsync(value);
    }

    partial void OnSearchTextChanged(string value) {
        if (_syncingSearchText)
            return;

        // Debounce: wait until typing pauses, so each keystroke doesn't re-sort thousands of items.
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _searchTask = ApplySearchAsync(value, cts.Token);
    }

    private async Task ApplySearchAsync(string text, CancellationToken cancellationToken) {
        try {
            if (_searchDelay > TimeSpan.Zero)
                await Task.Delay(_searchDelay, cancellationToken);
        } catch (OperationCanceledException) {
            return; // typed again: a newer search is on its way
        }

        if (!cancellationToken.IsCancellationRequested)
            ApplyQueryChange(_query with { Search = text.Trim() }, syncSearchText: false);
    }

    [RelayCommand]
    private void ToggleLayout() => IsGrid = !IsGrid;

    [RelayCommand]
    private Task RefreshAsync() => _loadTask = LoadAsync(isRefresh: true);

    [RelayCommand]
    private Task RetryAsync() => _loadTask = LoadAsync(isRefresh: false);

    [RelayCommand]
    private void SetFilter(FilterChipViewModel chip) => Query = _query with { Filter = chip.Filter };

    [RelayCommand]
    private void ShowSort()
        => _sheets.Show<SortPickerViewModel>(picker => picker.Load(_query.SortKey, _query.Descending, OnSortPicked));

    private void OnSortPicked(MediaSortKey key) {
        _sheets.Hide();

        // Picking the current sort again reverses it; picking another starts in that sort's natural direction.
        Query = key == _query.SortKey ? _query with { Descending = !_query.Descending } : _query.SortedBy(key);
    }

    /// <summary>Drops the search and filter (keeps the chosen sort).</summary>
    [RelayCommand]
    private void ClearFilters() => Query = _query with { Search = "", Filter = MediaFilter.All };

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
            UpdateFilterCounts();
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

    // A different server is a different library: start from a clean search, filter and sort.
    private void Reset() {
        _all = [];
        ErrorMessage = null;
        Items.Clear();
        _searchCts?.Cancel();
        _query = new MediaQuery();
        SetSearchTextQuietly("");
        UpdateFilterCounts();
        UpdateQueryState();
        RebuildRows();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(SearchPlaceholder));
        UpdateEmptyStates();
    }

    private void ApplyQueryChange(MediaQuery query, bool syncSearchText) {
        _query = query;
        if (syncSearchText)
            SetSearchTextQuietly(query.Search);
        ApplyQuery();
        UpdateQueryState();
    }

    private void SetSearchTextQuietly(string text) {
        _syncingSearchText = true;
        try {
            SearchText = text;
        } finally {
            _syncingSearchText = false;
        }
    }

    private void ApplyQuery() {
        var byId = _all.ToDictionary(card => card.Id);
        var ordered = MediaQueryEngine.Apply(_all.Select(card => card.Item), _query).Select(item => byId[item.Id]);

        Items.Clear();
        foreach (var card in ordered)
            Items.Add(card);

        RebuildRows();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(SearchPlaceholder));
        UpdateEmptyStates();
    }

    private void UpdateQueryState() {
        foreach (var chip in FilterChips)
            chip.IsSelected = chip.Filter == _query.Filter;

        OnPropertyChanged(nameof(SortChipText));
        OnPropertyChanged(nameof(IsNarrowed));
    }

    private void UpdateFilterCounts() {
        var items = _all.Select(card => card.Item).ToList();
        foreach (var chip in FilterChips)
            chip.Count = MediaQueryEngine.Count(items, chip.Filter);
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
