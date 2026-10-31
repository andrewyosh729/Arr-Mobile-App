namespace ArrApp.Core.Api;

/// <summary>Cleans up what a person types into the "server address" field.</summary>
public static class ArrUrl {
    /// <summary>
    /// Normalizes to <c>scheme://host[:port][/urlbase]</c> with no trailing slash. A missing scheme defaults to
    /// <c>http://</c> (typical for LAN servers); a pasted trailing <c>/api</c> or <c>/api/v3</c> is removed;
    /// URL-base subpaths such as <c>/sonarr</c> are kept; query and fragment are dropped.
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized, out string? error) {
        normalized = "";
        var text = input?.Trim() ?? "";
        if (text.Length == 0) {
            error = "Enter the server address.";
            return false;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
            text = "http://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)) {
            error = "That doesn't look like a valid address.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) {
            error = "The address must start with http:// or https://.";
            return false;
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        foreach (var suffix in new[] { "/api/v3", "/api" }) {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) {
                path = path[..^suffix.Length];
                break;
            }
        }

        normalized = uri.GetLeftPart(UriPartial.Authority) + path;
        error = null;
        return true;
    }
}
