using System;
using System.Threading.Tasks;
using ArrApp.Core;
using ArrApp.UI.Errors;
using ArrApp.UI.Navigation;
using ArrApp.UI.Theme;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

/// <summary>The "Sonarr ▾" pill in the top bar. Tapping it opens a sheet to switch the active server.</summary>
public sealed partial class ServerSwitcherViewModel : ViewModelBase {
    private readonly ArrContext _context;
    private readonly IServerConnectionRepository _repository;
    private readonly IBottomSheetService _sheets;
    private readonly INavigationService _navigation;
    private readonly IErrorReporter _errors;

    public ServerSwitcherViewModel(
        ArrContext context,
        IServerConnectionRepository repository,
        IBottomSheetService sheets,
        INavigationService navigation,
        IErrorReporter errors) {
        _context = context;
        _repository = repository;
        _sheets = sheets;
        _navigation = navigation;
        _errors = errors;

        // The switcher lives as long as the app, like the context it listens to.
        context.PropertyChanged += (_, _) => {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(TypeLabel));
            OnPropertyChanged(nameof(AccentBrush));
        };
    }

    public string Name => _context.Active?.Name ?? "No server";
    public string TypeLabel => _context.ArrType?.ToString() ?? "";
    public IBrush AccentBrush => new SolidColorBrush(AccentPalette.For(_context.ArrType));

    [RelayCommand]
    private async Task ShowAsync() {
        try {
            var servers = await _repository.GetAllAsync();
            _sheets.Show<ServerPickerViewModel>(picker => picker.Load(servers, _context.Active?.Id, SelectAsync, ManageAsync));
        } catch (Exception ex) {
            _errors.Report(ex, "Couldn't load your servers.");
        }
    }

    private async Task SelectAsync(ServerConnection server) {
        _sheets.Hide();
        try {
            await _context.SelectAsync(server);
        } catch (Exception ex) {
            _errors.Report(ex, "Couldn't switch servers.");
        }
    }

    private Task ManageAsync() {
        _sheets.Hide();
        _navigation.Push<ServersViewModel>(vm => _ = vm.LoadAsync());
        return Task.CompletedTask;
    }
}
