using System;
using System.Threading;
using System.Threading.Tasks;
using ArrApp.Core;
using ArrApp.Core.Api;
using ArrApp.UI.Errors;
using ArrApp.UI.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArrApp.UI.ViewModels;

public enum TestState {
    Idle,
    Testing,
    Succeeded,
    Failed,
}

/// <summary>
/// Add or edit a server. A server is saved only after a successful connection test of the values currently
/// on screen; after a failed test the user can still save explicitly ("Save anyway"), except for a malformed
/// address or a server that is the wrong app.
/// </summary>
public sealed partial class ServerEditViewModel(
    IServerConnectionRepository repository,
    IArrConnectionTester tester,
    INavigationService navigation,
    IToastService toasts,
    IErrorReporter errors) : ViewModelBase {
    private ServerConnection? _existing;
    private string? _verifiedFingerprint;
    private string? _verifiedVersion;
    private CancellationTokenSource? _testCts;

    /// <summary>Called after a successful save, so the caller can refresh its list.</summary>
    public Action? Saved { get; set; }

    public bool IsEdit => _existing is not null;
    public string Title => IsEdit ? "Edit server" : "Add server";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSonarr), nameof(IsRadarr))]
    private ArrType _arrType = ArrType.Sonarr;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private string _apiKey = "";

    [ObservableProperty]
    private bool _showApiKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTesting), nameof(IsSuccess), nameof(IsFailed), nameof(HasStatus), nameof(IsBusy))]
    private TestState _state;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _canSaveAnyway;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isSaving;

    public bool IsTesting => State == TestState.Testing;
    public bool IsSuccess => State == TestState.Succeeded;
    public bool IsFailed => State == TestState.Failed;
    public bool HasStatus => State != TestState.Idle;
    public bool IsBusy => IsTesting || IsSaving;

    public string NamePlaceholder => ArrType.ToString();

    // Two-way friendly for the type chips: setting false is ignored so one chip always stays selected.
    public bool IsSonarr {
        get => ArrType == ArrType.Sonarr;
        set { if (value) ArrType = ArrType.Sonarr; else OnPropertyChanged(); }
    }

    public bool IsRadarr {
        get => ArrType == ArrType.Radarr;
        set { if (value) ArrType = ArrType.Radarr; else OnPropertyChanged(); }
    }

    /// <summary>Switch to edit mode for an existing server.</summary>
    public void Load(ServerConnection connection) {
        _existing = connection;
        ArrType = connection.ArrType;
        Name = connection.Name;
        Url = connection.BaseUrl;
        ApiKey = connection.ApiKey;
        ResetTest();
        OnPropertyChanged(nameof(IsEdit));
        OnPropertyChanged(nameof(Title));
    }

    // Any edit makes an earlier test result meaningless.
    partial void OnArrTypeChanged(ArrType value) {
        OnPropertyChanged(nameof(NamePlaceholder));
        ResetTest();
    }

    partial void OnUrlChanged(string value) => ResetTest();
    partial void OnApiKeyChanged(string value) => ResetTest();

    private void ResetTest() {
        _testCts?.Cancel();
        _verifiedFingerprint = null;
        _verifiedVersion = null;
        State = TestState.Idle;
        StatusMessage = "";
        CanSaveAnyway = false;
    }

    private bool TryValidate(out string baseUrl, out string key) {
        key = ApiKey.Trim();
        if (!ArrUrl.TryNormalize(Url, out baseUrl, out var urlError)) {
            Fail(urlError!, canSaveAnyway: false);
            return false;
        }

        if (key.Length == 0) {
            Fail("Enter the API key. You can find it in the Arr's web UI under Settings → General → Security.", canSaveAnyway: false);
            return false;
        }

        return true;
    }

    private void Fail(string message, bool canSaveAnyway) {
        State = TestState.Failed;
        StatusMessage = message;
        CanSaveAnyway = canSaveAnyway;
    }

    private string Fingerprint(string baseUrl, string key) => $"{ArrType}|{baseUrl}|{key}";

    /// <returns>The test result, or null when the input was invalid or the test was superseded.</returns>
    private async Task<ConnectionTestResult?> RunTestAsync() {
        if (!TryValidate(out var baseUrl, out var key))
            return null;

        _testCts?.Cancel();
        var cts = _testCts = new CancellationTokenSource();
        var fingerprint = Fingerprint(baseUrl, key);

        State = TestState.Testing;
        StatusMessage = "Testing connection…";
        CanSaveAnyway = false;

        ConnectionTestResult result;
        try {
            result = await tester.TestAsync(ArrType, baseUrl, key, cts.Token);
        } catch (OperationCanceledException) {
            return null; // superseded by a newer test, or the user edited a field
        }

        if (cts.IsCancellationRequested)
            return null;

        if (result.Success) {
            _verifiedFingerprint = fingerprint;
            _verifiedVersion = result.Version;
            State = TestState.Succeeded;
            StatusMessage = result.Message;
        } else {
            Fail(result.Message, canSaveAnyway: result.Error is not (ArrErrorKind.InvalidUrl or ArrErrorKind.WrongApp));
        }

        return result;
    }

    [RelayCommand]
    private async Task TestConnectionAsync() {
        try {
            await RunTestAsync();
        } catch (Exception ex) {
            errors.Report(ex);
            ResetTest();
        }
    }

    [RelayCommand]
    private async Task SaveAsync() {
        if (IsSaving)
            return;

        try {
            if (!TryValidate(out var baseUrl, out var key))
                return;

            string? version;
            if (_verifiedFingerprint == Fingerprint(baseUrl, key)) {
                version = _verifiedVersion; // already tested with exactly these values; no second round trip
            } else {
                var result = await RunTestAsync();
                if (result is not { Success: true })
                    return; // the failure (and "Save anyway" when allowed) is already on screen
                version = result.Version;
            }

            await PersistAsync(baseUrl, key, version);
        } catch (Exception ex) {
            errors.Report(ex, "Couldn't save the server.");
        }
    }

    [RelayCommand]
    private async Task SaveAnywayAsync() {
        if (IsSaving || !TryValidate(out var baseUrl, out var key))
            return;

        try {
            await PersistAsync(baseUrl, key, version: null);
        } catch (Exception ex) {
            errors.Report(ex, "Couldn't save the server.");
        }
    }

    private async Task PersistAsync(string baseUrl, string key, string? version) {
        IsSaving = true;
        try {
            var connection = _existing ?? new ServerConnection();
            connection.ArrType = ArrType;
            connection.Name = string.IsNullOrWhiteSpace(Name) ? ArrType.ToString() : Name.Trim();
            connection.BaseUrl = baseUrl;
            connection.ApiKey = key;

            if (IsEdit)
                await repository.UpdateAsync(connection);
            else
                await repository.AddAsync(connection);

            if (version is not null)
                await repository.MarkConnectedAsync(connection.Id, version, DateTimeOffset.UtcNow);

            toasts.Show($"Saved {connection.Name}", ToastKind.Success);
            navigation.Pop();
            Saved?.Invoke();
        } finally {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Back() {
        _testCts?.Cancel();
        navigation.Pop();
    }
}
