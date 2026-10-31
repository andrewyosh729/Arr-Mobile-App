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

/// <summary>Main screen: the Arr switcher in the top bar, the active tab's content, and the bottom tab bar.</summary>
public sealed partial class TabHostViewModel : ViewModelBase {
    public TabHostViewModel(IEnumerable<TabItem> tabs, ServerSwitcherViewModel switcher) {
        Switcher = switcher;
        Tabs = [.. tabs];
        if (Tabs.Count > 0)
            SelectTab(Tabs[0]);
    }

    public ServerSwitcherViewModel Switcher { get; }

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
