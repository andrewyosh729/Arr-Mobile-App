using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ArrApp.Core;
using ArrApp.UI.Theme;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

public sealed class PickerItemViewModel(ServerConnection server, bool isActive) {
    public ServerConnection Server { get; } = server;
    public string Name => Server.Name;
    public string Url => Server.BaseUrl;
    public string Initial => Server.ArrType.ToString()[..1];
    public bool IsActive { get; } = isActive;
    public IBrush AccentBrush { get; } = new SolidColorBrush(AccentPalette.For(server.ArrType));
}

/// <summary>Bottom-sheet content for the Arr switcher: pick a server, or jump to server management.</summary>
public sealed partial class ServerPickerViewModel : ViewModelBase {
    private Func<ServerConnection, Task>? _onSelect;
    private Func<Task>? _onManage;

    public ObservableCollection<PickerItemViewModel> Items { get; } = [];

    public void Load(IEnumerable<ServerConnection> servers, int? activeId, Func<ServerConnection, Task> onSelect, Func<Task> onManage) {
        _onSelect = onSelect;
        _onManage = onManage;
        Items.Clear();
        foreach (var server in servers)
            Items.Add(new PickerItemViewModel(server, server.Id == activeId));
    }

    [RelayCommand]
    private async Task SelectAsync(PickerItemViewModel item) {
        if (_onSelect is not null)
            await _onSelect(item.Server);
    }

    [RelayCommand]
    private async Task ManageAsync() {
        if (_onManage is not null)
            await _onManage();
    }
}
