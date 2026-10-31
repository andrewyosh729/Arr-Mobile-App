namespace ArrApp.Core.Api;

public enum ArrErrorKind {
    InvalidUrl,
    Unauthorized,
    NotFound,
    Unreachable,
    Timeout,
    Tls,
    ServerError,
    InvalidResponse,
    WrongApp,
}

/// <summary>An expected failure talking to an Arr. <see cref="Exception.Message"/> is safe to show to the user (never contains the API key).</summary>
public sealed class ArrApiException(ArrErrorKind kind, string message, Exception? inner = null) : Exception(message, inner) {
    public ArrErrorKind Kind { get; } = kind;

    public static ArrApiException For(ArrErrorKind kind, Exception? inner = null, int? statusCode = null) => new(kind, kind switch {
        ArrErrorKind.Unauthorized => "The server rejected the API key. Check it under Settings → General → Security in the Arr's web UI.",
        ArrErrorKind.NotFound => "Nothing responded at that address. If the server uses a URL base (like /sonarr), include it.",
        ArrErrorKind.Unreachable => "Couldn't reach the server. Check the address and port, and that you're on the right network.",
        ArrErrorKind.Timeout => "The server took too long to respond.",
        ArrErrorKind.Tls => "Couldn't establish a secure connection. Check the server's certificate, or use http:// on a trusted network.",
        ArrErrorKind.ServerError => $"The server reported an error{(statusCode is { } c ? $" ({c})" : "")}. Try again in a moment.",
        ArrErrorKind.InvalidResponse => "That address responded, but not like Sonarr or Radarr. Check the URL, including any URL base.",
        _ => "Something went wrong talking to the server.",
    }, inner);
}
