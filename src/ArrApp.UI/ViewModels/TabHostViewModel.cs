using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

public sealed record TabItem(string Title, ViewModelBase Content);

/// <summary>Bottom-tab host. Tab contents are placeholders until the real screens land.</summary>
public sealed partial class TabHostViewModel : ViewModelBase {
    public TabHostViewModel(IEnumerable<TabItem> tabs) {
        Tabs = [.. tabs];
        SelectedTab = Tabs.Count > 0 ? Tabs[0] : null;
    }

    public IReadOnlyList<TabItem> Tabs { get; }

    [ObservableProperty]
    private TabItem? _selectedTab;

    [RelayCommand]
    private void SelectTab(TabItem tab) => SelectedTab = tab;
}
