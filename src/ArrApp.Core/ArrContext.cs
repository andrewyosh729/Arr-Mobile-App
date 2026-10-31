using System.ComponentModel;

namespace ArrApp.Core;

/// <summary>
/// The active server: which Sonarr/Radarr the app is currently showing. The UI binds to it to decide what a screen
/// means ("Library" is series or movies), which accent to use and which client to call.
/// Starts on the default server; switching and remembering the last choice come with F-108.
/// </summary>
public sealed class ArrContext(IServerConnectionRepository repository) : INotifyPropertyChanged {
    private ServerConnection? _active;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ServerConnection? Active => _active;
    public ArrType? ArrType => _active?.ArrType;
    public string ActiveLabel => _active is null ? "No server" : $"{_active.Name} · {_active.ArrType}";

    /// <summary>Picks the default server (or the first saved one). Leaves <see cref="Active"/> null when none are saved.</summary>
    public Task InitializeAsync() => RefreshAsync();

    /// <summary>
    /// Re-reads storage after servers were added, edited or deleted: the active server is kept (with its latest
    /// values) while it still exists, otherwise the default takes over.
    /// </summary>
    public async Task RefreshAsync() {
        var all = await repository.GetAllAsync();
        var current = _active is null ? null : all.FirstOrDefault(s => s.Id == _active.Id);
        SetActive(current ?? all.FirstOrDefault(s => s.IsDefault) ?? all.FirstOrDefault());
    }

    public void SetActive(ServerConnection? server) {
        var changed = _active?.Id != server?.Id;
        _active = server;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveLabel)));
        if (changed) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Active)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ArrType)));
        }
    }
}
