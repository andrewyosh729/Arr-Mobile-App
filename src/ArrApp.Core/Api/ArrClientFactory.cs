namespace ArrApp.Core.Api;

public interface IArrClientFactory {
    /// <summary>A client for whichever Arr the server is.</summary>
    IArrClient Create(ServerConnection server);

    ISonarrClient CreateSonarr(ServerConnection server);
    IRadarrClient CreateRadarr(ServerConnection server);
}

/// <summary>Builds clients for saved servers over one shared <see cref="HttpClient"/> (the API key is sent per request).</summary>
public sealed class ArrClientFactory(HttpClient http) : IArrClientFactory {
    public IArrClient Create(ServerConnection server) => server.ArrType switch {
        ArrType.Sonarr => CreateSonarr(server),
        ArrType.Radarr => CreateRadarr(server),
        _ => throw new ArgumentOutOfRangeException(nameof(server), server.ArrType, "Unsupported server type."),
    };

    public ISonarrClient CreateSonarr(ServerConnection server) {
        Require(server, ArrType.Sonarr);
        return new SonarrClient(new ArrHttpClient(http, server.BaseUrl, server.ApiKey));
    }

    public IRadarrClient CreateRadarr(ServerConnection server) {
        Require(server, ArrType.Radarr);
        return new RadarrClient(new ArrHttpClient(http, server.BaseUrl, server.ApiKey));
    }

    private static void Require(ServerConnection server, ArrType expected) {
        if (server.ArrType != expected)
            throw new ArgumentException($"'{server.Name}' is a {server.ArrType} server, not {expected}.", nameof(server));
    }
}
