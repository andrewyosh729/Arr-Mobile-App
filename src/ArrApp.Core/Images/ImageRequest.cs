using ArrApp.Core.Api;
using ArrApp.Core.Models;

namespace ArrApp.Core.Images;

/// <summary>
/// One place an image can be fetched from. <see cref="ApiKey"/> is set only for the user's own server, and is never
/// part of <see cref="ToString"/> (a record would print it).
/// </summary>
public sealed class ImageCandidate(string url, string? apiKey) {
    public string Url { get; } = url;
    public string? ApiKey { get; } = apiKey;
    public bool SendApiKey => ApiKey is not null;

    public override string ToString() => Url;
}

/// <summary>
/// Describes where to get an image: the Arr server's own <c>MediaCover</c> path (needs the API key) and/or the public
/// remote URL (TMDB/TVDB, never sent the key). Deliberately not a record: its default <c>ToString</c> would print the
/// API key into logs.
/// </summary>
public sealed class ImageRequest {
    public ImageRequest(string? serverBaseUrl, string? apiKey, string? serverPath, string? remoteUrl) {
        ServerBaseUrl = serverBaseUrl;
        ApiKey = apiKey;
        ServerPath = serverPath;
        RemoteUrl = remoteUrl;
    }

    public string? ServerBaseUrl { get; }
    public string? ApiKey { get; }
    public string? ServerPath { get; }
    public string? RemoteUrl { get; }

    public static ImageRequest For(ServerConnection server, MediaImage image)
        => new(server.BaseUrl, server.ApiKey, image.Url, image.RemoteUrl);

    /// <summary>Where to try, in order: the server first (it caches artwork and works offline from the internet), then the public URL.</summary>
    public IReadOnlyList<ImageCandidate> Candidates() {
        var list = new List<ImageCandidate>(2);

        if (!string.IsNullOrWhiteSpace(ServerPath)) {
            if (IsAbsoluteHttp(ServerPath)) {
                // An absolute URL from the server's JSON may point anywhere: never attach the key to it.
                list.Add(new ImageCandidate(ServerPath, apiKey: null));
            } else if (!string.IsNullOrWhiteSpace(ServerBaseUrl) && ResolveServerUrl(ServerBaseUrl, ServerPath) is { } url) {
                list.Add(new ImageCandidate(url, string.IsNullOrEmpty(ApiKey) ? null : ApiKey));
            }
        }

        if (IsAbsoluteHttp(RemoteUrl) && list.All(c => c.Url != RemoteUrl))
            list.Add(new ImageCandidate(RemoteUrl!, apiKey: null));

        return list;
    }

    /// <summary>
    /// Joins the server address and an image path. With a URL base configured (<c>/sonarr</c>) the Arr usually returns
    /// image paths that already include it (<c>/sonarr/MediaCover/...</c>), so it must not be added twice.
    /// </summary>
    public static string? ResolveServerUrl(string serverBaseUrl, string imagePath) {
        if (!ArrUrl.TryNormalize(serverBaseUrl, out var root, out _))
            return null;

        var path = imagePath.StartsWith('/') ? imagePath : "/" + imagePath;
        var uri = new Uri(root);
        var basePath = uri.AbsolutePath.TrimEnd('/');

        return basePath.Length > 0 && path.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase)
            ? uri.GetLeftPart(UriPartial.Authority) + path
            : root + path;
    }

    private static bool IsAbsoluteHttp(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public override string ToString() => $"ImageRequest({ServerPath ?? RemoteUrl ?? "empty"})";
}
