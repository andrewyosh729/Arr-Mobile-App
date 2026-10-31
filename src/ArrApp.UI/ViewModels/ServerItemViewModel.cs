using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.UI.Theme;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArrApp.UI.ViewModels;

public enum ServerStatus {
    Checking,
    Online,
    Offline,
}

/// <summary>One row in the servers list: the saved server plus its live status.</summary>
public sealed partial class ServerItemViewModel(ServerConnection connection) : ObservableObject {
    public ServerConnection Connection { get; } = connection;

    public int Id => Connection.Id;
    public string Name => Connection.Name;
    public string Url => Connection.BaseUrl;
    public ArrType ArrType => Connection.ArrType;
    public string TypeLabel => ArrType.ToString();
    public string Initial => TypeLabel[..1];
    public IBrush AccentBrush { get; } = new SolidColorBrush(AccentPalette.For(connection.ArrType));

    [ObservableProperty]
    private bool _isDefault = connection.IsDefault;

    [ObservableProperty]
    private bool _canMoveUp;

    [ObservableProperty]
    private bool _canMoveDown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnline), nameof(IsOffline), nameof(IsChecking))]
    private ServerStatus _status = ServerStatus.Checking;

    [ObservableProperty]
    private string _statusText = "Checking…";

    public bool IsOnline => Status == ServerStatus.Online;
    public bool IsOffline => Status == ServerStatus.Offline;
    public bool IsChecking => Status == ServerStatus.Checking;
    public bool IsNotDefault => !IsDefault;

    partial void OnIsDefaultChanged(bool value) => OnPropertyChanged(nameof(IsNotDefault));

    public void SetChecking() {
        Status = ServerStatus.Checking;
        StatusText = "Checking…";
    }

    public void SetNeedsApiKey() {
        Status = ServerStatus.Offline;
        StatusText = "Re-enter API key";
    }

    public void SetResult(ConnectionTestResult result) {
        if (result.Success) {
            Status = ServerStatus.Online;
            StatusText = result.Version is null ? "Online" : $"Online · v{result.Version}";
        } else {
            Status = ServerStatus.Offline;
            StatusText = ShortLabel(result.Error);
        }
    }

    /// <summary>A copy for editing, so a cancelled or failed edit never changes what the list shows.</summary>
    public ServerConnection CloneConnection() => new() {
        Id = Connection.Id,
        Name = Connection.Name,
        ArrType = Connection.ArrType,
        BaseUrl = Connection.BaseUrl,
        ApiKey = Connection.ApiKey,
        IsDefault = Connection.IsDefault,
        SortOrder = Connection.SortOrder,
        CreatedAt = Connection.CreatedAt,
        LastConnectedAt = Connection.LastConnectedAt,
        LastKnownVersion = Connection.LastKnownVersion,
    };

    public static string ShortLabel(ArrErrorKind? kind) => kind switch {
        ArrErrorKind.Unauthorized => "Invalid API key",
        ArrErrorKind.Unreachable => "Unreachable",
        ArrErrorKind.Timeout => "Timed out",
        ArrErrorKind.Tls => "Certificate problem",
        ArrErrorKind.NotFound => "Not found at this address",
        ArrErrorKind.WrongApp => "Wrong app for this type",
        ArrErrorKind.InvalidResponse => "Not Sonarr or Radarr",
        ArrErrorKind.ServerError => "Server error",
        ArrErrorKind.InvalidUrl => "Invalid address",
        _ => "Offline",
    };
}
