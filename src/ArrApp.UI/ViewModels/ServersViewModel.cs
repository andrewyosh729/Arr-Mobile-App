using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.UI.Errors;
using ArrApp.UI.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

/// <summary>Saved servers: live status, edit, set default, reorder and delete (with confirmation).</summary>
public sealed partial class ServersViewModel(
    IServerConnectionRepository repository,
    IArrConnectionTester tester,
    INavigationService navigation,
    IBottomSheetService sheets,
    IToastService toasts,
    IErrorReporter errors) : ViewModelBase {
    private CancellationTokenSource? _statusCts;

    public ObservableCollection<ServerItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>Reloads the list from storage, then re-checks every server in the background.</summary>
    public async Task LoadAsync() {
        try {
            var all = await repository.GetAllAsync();
            Items.Clear();
            foreach (var server in all)
                Items.Add(new ServerItemViewModel(server));
            UpdateOrderState();
            _ = RefreshStatusesAsync();
        } catch (Exception ex) {
            errors.Report(ex, "Couldn't load your servers.");
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => RefreshStatusesAsync();

    private async Task RefreshStatusesAsync() {
        _statusCts?.Cancel();
        var cts = _statusCts = new CancellationTokenSource();
        var items = Items.ToList();
        foreach (var item in items)
            item.SetChecking();

        await Task.WhenAll(items.Select(item => CheckAsync(item, cts.Token)));
    }

    private async Task CheckAsync(ServerItemViewModel item, CancellationToken ct) {
        try {
            var connection = item.Connection;
            if (connection.ApiKey.Length == 0) {
                // The stored key could no longer be decrypted (see the repository); don't bother the server with it.
                item.SetNeedsApiKey();
                return;
            }

            var result = await tester.TestAsync(connection.ArrType, connection.BaseUrl, connection.ApiKey, ct);
            if (ct.IsCancellationRequested)
                return;

            item.SetResult(result);
            if (result is { Success: true, Version: { } version })
                await repository.MarkConnectedAsync(item.Id, version, DateTimeOffset.UtcNow);
        } catch (OperationCanceledException) {
            // superseded by a newer refresh, or the list was reloaded
        } catch (Exception ex) {
            errors.Report(ex);
        }
    }

    [RelayCommand]
    private void Add() => navigation.Push<ServerEditViewModel>(vm => vm.Saved = () => _ = LoadAsync());

    [RelayCommand]
    private void Edit(ServerItemViewModel item)
        => navigation.Push<ServerEditViewModel>(vm => {
            vm.Load(item.CloneConnection());
            vm.Saved = () => _ = LoadAsync();
        });

    [RelayCommand]
    private async Task SetDefaultAsync(ServerItemViewModel item) {
        try {
            await repository.SetDefaultAsync(item.Id);
            foreach (var other in Items)
                other.IsDefault = ReferenceEquals(other, item);
            toasts.Show($"{item.Name} is now the default server", ToastKind.Success);
        } catch (Exception ex) {
            errors.Report(ex, "Couldn't change the default server.");
        }
    }

    [RelayCommand]
    private Task MoveUpAsync(ServerItemViewModel item) => MoveAsync(item, -1);

    [RelayCommand]
    private Task MoveDownAsync(ServerItemViewModel item) => MoveAsync(item, +1);

    private async Task MoveAsync(ServerItemViewModel item, int delta) {
        var from = Items.IndexOf(item);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= Items.Count)
            return;

        try {
            var order = Items.Select(i => i.Id).ToList();
            order.RemoveAt(from);
            order.Insert(to, item.Id);
            await repository.ReorderAsync(order);

            Items.Move(from, to);
            UpdateOrderState();
        } catch (Exception ex) {
            errors.Report(ex, "Couldn't reorder the servers.");
        }
    }

    [RelayCommand]
    private void Delete(ServerItemViewModel item)
        => sheets.Show<ConfirmViewModel>(confirm => {
            confirm.Title = $"Delete {item.Name}?";
            confirm.Message = "This removes the server from this app. Your Sonarr/Radarr server and its data aren't touched.";
            confirm.ConfirmText = "Delete";
            confirm.IsDestructive = true;
            confirm.OnConfirm = () => DeleteConfirmedAsync(item);
        });

    private async Task DeleteConfirmedAsync(ServerItemViewModel item) {
        await repository.DeleteAsync(item.Id);
        toasts.Show($"Deleted {item.Name}");
        await LoadAsync();
    }

    [RelayCommand]
    private void Back() {
        _statusCts?.Cancel();
        navigation.Pop();
    }

    private void UpdateOrderState() {
        for (var i = 0; i < Items.Count; i++) {
            Items[i].CanMoveUp = i > 0;
            Items[i].CanMoveDown = i < Items.Count - 1;
        }

        IsEmpty = Items.Count == 0;
    }
}
