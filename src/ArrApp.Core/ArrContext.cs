using System.ComponentModel;

namespace ArrApp.Core;

/// <summary>
/// The active server: which Sonarr/Radarr the app is currently showing. The UI binds to it to decide what a screen
/// means ("Library" is series or movies), which accent to use and which client to call. The last choice is remembered
/// across launches; with nothing remembered (or the remembered server gone) the default server is used.
/// </summary>
public sealed class ArrContext(IServerConnectionRepository repository, ISettingsStore settings) : INotifyPropertyChanged {
    public const string LastActiveServerKey = "LastActiveServerId";

    private ServerConnection? _active;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ServerConnection? Active => _active;
    public ArrType? ArrType => _active?.ArrType;
    public string ActiveLabel => _active is null ? "No server" : $"{_active.Name} · {_active.ArrType}";

    /// <summary>What this Arr manages, for screen wording.</summary>
    public string MediaNoun => _active?.ArrType switch {
        Core.ArrType.Sonarr => "Series",
        Core.ArrType.Radarr => "Movies",
        _ => "",
    };

    /// <summary>Picks the remembered server, else the default one, else the first saved. Leaves <see cref="Active"/> null when none are saved.</summary>
    public async Task InitializeAsync() {
        var all = await repository.GetAllAsync();
        SetActive(await RememberedAsync(all) ?? DefaultOf(all));
    }

    /// <summary>
    /// Re-reads storage after servers were added, edited or deleted: the active server is kept (with its latest
    /// values) while it still exists, otherwise the default takes over.
    /// </summary>
    public async Task RefreshAsync() {
        var all = await repository.GetAllAsync();
        var current = _active is null ? null : all.FirstOrDefault(s => s.Id == _active.Id);
        SetActive(current ?? DefaultOf(all));
    }

    /// <summary>Switches to <paramref name="server"/> and remembers it for the next launch.</summary>
    public async Task SelectAsync(ServerConnection server) {
        SetActive(server);
        await settings.SetAsync(LastActiveServerKey, server.Id.ToString());
    }

    public void SetActive(ServerConnection? server) {
        var changed = _active?.Id != server?.Id;
        _active = server;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveLabel)));
        if (changed) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Active)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ArrType)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MediaNoun)));
        }
    }

    private static ServerConnection? DefaultOf(IReadOnlyList<ServerConnection> all)
        => all.FirstOrDefault(s => s.IsDefault) ?? all.FirstOrDefault();

    private async Task<ServerConnection?> RememberedAsync(IReadOnlyList<ServerConnection> all) {
        try {
            var text = await settings.GetAsync(LastActiveServerKey);
            return int.TryParse(text, out var id) ? all.FirstOrDefault(s => s.Id == id) : null;
        } catch (Exception) {
            return null; // a settings problem must never stop the app from opening on the default server
        }
    }
}
