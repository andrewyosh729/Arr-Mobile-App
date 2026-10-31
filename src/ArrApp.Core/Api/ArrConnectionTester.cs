namespace ArrApp.Core.Api;

public sealed record ConnectionTestResult(bool Success, string Message, string? AppName = null, string? Version = null, ArrErrorKind? Error = null) {
    public static ConnectionTestResult Ok(string appName, string? version)
        => new(true, version is null ? $"Connected to {appName}." : $"Connected to {appName} {version}.", appName, version);

    public static ConnectionTestResult Fail(ArrErrorKind kind, string message, string? appName = null)
        => new(false, message, appName, null, kind);
}

public interface IArrConnectionTester {
    Task<ConnectionTestResult> TestAsync(ArrType expected, string baseUrl, string apiKey, CancellationToken cancellationToken = default);
}

/// <summary>Calls <c>GET /api/v3/system/status</c> and checks the server really is the Arr the user picked.</summary>
public sealed class ArrConnectionTester(HttpClient http) : IArrConnectionTester {
    public async Task<ConnectionTestResult> TestAsync(ArrType expected, string baseUrl, string apiKey, CancellationToken cancellationToken = default) {
        SystemStatus status;
        try {
            status = await new ArrHttpClient(http, baseUrl, apiKey)
                .GetAsync("system/status", ArrJsonContext.Default.SystemStatus, cancellationToken).ConfigureAwait(false);
        } catch (ArrApiException ex) {
            return ConnectionTestResult.Fail(ex.Kind, ex.Message);
        }

        var appName = status.AppName?.Trim();
        if (string.IsNullOrEmpty(appName))
            return ConnectionTestResult.Fail(ArrErrorKind.InvalidResponse, ArrApiException.For(ArrErrorKind.InvalidResponse).Message);

        if (!appName.Equals(expected.ToString(), StringComparison.OrdinalIgnoreCase))
            return ConnectionTestResult.Fail(ArrErrorKind.WrongApp, $"This server is {appName}, not {expected}. Pick the matching type or check the address.", appName);

        return ConnectionTestResult.Ok(appName, status.Version);
    }
}
