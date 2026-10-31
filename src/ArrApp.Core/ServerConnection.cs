namespace ArrApp.Core;

/// <summary>A saved Sonarr/Radarr server ("account").</summary>
public sealed class ServerConnection {
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ArrType ArrType { get; set; }

    /// <summary>As entered/normalized by the HTTP layer, e.g. <c>https://host:8989/sonarr</c>.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Plain text in memory only; always passed through <see cref="IApiKeyProtector"/> before it is stored.</summary>
    public string ApiKey { get; set; } = "";

    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastConnectedAt { get; set; }
    public string? LastKnownVersion { get; set; }
}
