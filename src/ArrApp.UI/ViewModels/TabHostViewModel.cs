using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

public sealed partial class TabItem(string title, ViewModelBase content) : ObservableObject {
    public string Title { get; } = title;
    public ViewModelBase Content { get; } = content;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Bottom-tab host. Tab contents are placeholders until the real screens land.</summary>
public sealed partial class TabHostViewModel : ViewModelBase {
    public TabHostViewModel(IEnumerable<TabItem> tabs) {
        Tabs = [.. tabs];
        if (Tabs.Count > 0)
            SelectTab(Tabs[0]);
    }

    public IReadOnlyList<TabItem> Tabs { get; }

    [ObservableProperty]
    private TabItem? _selectedTab;

    [RelayCommand]
    private void SelectTab(TabItem tab) {
        if (SelectedTab is { } old)
            old.IsSelected = false;
        tab.IsSelected = true;
        SelectedTab = tab;
    }
}
