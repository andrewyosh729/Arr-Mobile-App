using System;
using System.Collections.ObjectModel;
using ArrApp.Core.Presentation;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

public sealed class SortOptionViewModel(MediaSortKey key, bool isSelected, bool descending) {
    public MediaSortKey Key { get; } = key;
    public string Name => MediaSortText.Name(Key);
    public bool IsSelected { get; } = isSelected;

    /// <summary>The current direction for the selected option, otherwise the direction picking it would give.</summary>
    public string Detail { get; } = MediaSortText.Direction(key, descending);

    /// <summary>Hint on the selected option: picking it again flips the order.</summary>
    public string Hint => IsSelected ? "Tap again to reverse" : "";
}

/// <summary>Bottom-sheet content for the Sort chip.</summary>
public sealed partial class SortPickerViewModel : ViewModelBase {
    private Action<MediaSortKey>? _onPick;

    public ObservableCollection<SortOptionViewModel> Options { get; } = [];

    public void Load(MediaSortKey current, bool descending, Action<MediaSortKey> onPick) {
        _onPick = onPick;
        Options.Clear();
        foreach (var key in Enum.GetValues<MediaSortKey>()) {
            var isCurrent = key == current;
            Options.Add(new SortOptionViewModel(key, isCurrent, isCurrent ? descending : MediaQuery.DefaultDescending(key)));
        }
    }

    [RelayCommand]
    private void Pick(SortOptionViewModel option) => _onPick?.Invoke(option.Key);
}
