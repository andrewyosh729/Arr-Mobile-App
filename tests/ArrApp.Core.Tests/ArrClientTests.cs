using System.Net;
using ArrApp.Core.Api;
using ArrApp.Core.Models;

namespace ArrApp.Core.Tests;

/// <summary>Clients against a fake server that answers each endpoint with the fixture JSON.</summary>
public class ArrClientTests {
    private const string UrlBase = "/arr";

    /// <summary>The API paths requested, without the URL base.</summary>
    private static IEnumerable<string> ApiPaths(FakeHandler handler) => handler.Requests.Select(r => r.RequestUri!.PathAndQuery[UrlBase.Length..]);

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static FakeHandler FakeServer() => new((request, _) => {
        var path = request.RequestUri!.PathAndQuery;
        if (path.StartsWith(UrlBase + "/", StringComparison.Ordinal))
            path = path[UrlBase.Length..]; // like a reverse proxy serving the Arr under /arr
        var body = path switch {
            "/api/v3/series" => Fixture("series.json"),
            "/api/v3/series/12" => System.Text.Json.JsonDocument.Parse(Fixture("series.json")).RootElement[0].GetRawText(),
            "/api/v3/episode?seriesId=12" => Fixture("episodes.json"),
            "/api/v3/movie" => Fixture("movies.json"),
            "/api/v3/movie/7" => System.Text.Json.JsonDocument.Parse(Fixture("movies.json")).RootElement[0].GetRawText(),
            "/api/v3/qualityprofile" => Fixture("qualityprofiles.json"),
            "/api/v3/rootfolder" => Fixture("rootfolders.json"),
            "/api/v3/tag" => Fixture("tags.json"),
            _ => null,
        };
        return Task.FromResult(body is null
            ? FakeHandler.Response("{}", HttpStatusCode.NotFound)
            : FakeHandler.Response(body));
    });

    private static ServerConnection Server(ArrType type) => new() {
        Id = 1, Name = "Home", ArrType = type, BaseUrl = "http://nas:8989/arr", ApiKey = "KEY",
    };

    private static (IArrClientFactory Factory, FakeHandler Handler) Setup() {
        var handler = FakeServer();
        return (new ArrClientFactory(new HttpClient(handler)), handler);
    }

    // ---- Sonarr ----

    [Fact]
    public async Task Sonarr_Series_List_Hits_The_Right_Endpoint_With_The_Key_And_UrlBase() {
        var (factory, handler) = Setup();

        var series = await factory.CreateSonarr(Server(ArrType.Sonarr)).GetSeriesAsync();

        Assert.Equal(["Severance", "Barely There"], series.Select(s => s.Title));
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://nas:8989/arr/api/v3/series", request.RequestUri!.ToString());
        Assert.Equal("KEY", Assert.Single(request.Headers.GetValues("X-Api-Key")));
    }

    [Fact]
    public async Task Sonarr_Single_Series_And_Episodes() {
        var (factory, handler) = Setup();
        var client = factory.CreateSonarr(Server(ArrType.Sonarr));

        var series = await client.GetSeriesByIdAsync(12);
        var episodes = await client.GetEpisodesAsync(12);

        Assert.Equal("Severance", series.Title);
        Assert.Equal(3, episodes.Count);
        Assert.Equal(["/api/v3/series/12", "/api/v3/episode?seriesId=12"], ApiPaths(handler));
    }

    // ---- Radarr ----

    [Fact]
    public async Task Radarr_Movie_List_And_Single_Movie() {
        var (factory, handler) = Setup();
        var client = factory.CreateRadarr(Server(ArrType.Radarr));

        var movies = await client.GetMoviesAsync();
        var movie = await client.GetMovieAsync(7);

        Assert.Equal(["Dune: Part Two", "Not Out Yet"], movies.Select(m => m.Title));
        Assert.Equal("Dune: Part Two", movie.Title);
        Assert.Equal(["/api/v3/movie", "/api/v3/movie/7"], ApiPaths(handler));
    }

    // ---- Shared interface ----

    [Theory]
    [InlineData(ArrType.Sonarr, 2, "Severance")]
    [InlineData(ArrType.Radarr, 2, "Dune: Part Two")]
    public async Task The_Shared_Interface_Works_For_Both_Arrs(ArrType type, int count, string firstTitle) {
        var (factory, _) = Setup();
        IArrClient client = factory.Create(Server(type));

        var library = await client.GetLibraryAsync();

        Assert.Equal(type, client.Type);
        Assert.Equal(count, library.Count);
        Assert.Equal(firstTitle, library[0].Title);
        Assert.NotNull(library[0].Poster);
    }

    [Theory]
    [InlineData(ArrType.Sonarr, 12, "Severance")]
    [InlineData(ArrType.Radarr, 7, "Dune: Part Two")]
    public async Task GetItem_Returns_The_Shared_Base_Type(ArrType type, int id, string title) {
        var (factory, _) = Setup();

        MediaItem item = await factory.Create(Server(type)).GetItemAsync(id);

        Assert.Equal(title, item.Title);
        Assert.Equal(id, item.Id);
    }

    [Theory]
    [InlineData(ArrType.Sonarr)]
    [InlineData(ArrType.Radarr)]
    public async Task Profiles_Root_Folders_And_Tags_Are_The_Same_Calls_For_Both(ArrType type) {
        var (factory, handler) = Setup();
        var client = factory.Create(Server(type));

        var profiles = await client.GetQualityProfilesAsync();
        var roots = await client.GetRootFoldersAsync();
        var tags = await client.GetTagsAsync();

        Assert.Equal(["Any", "HD-1080p"], profiles.Select(p => p.Name));
        Assert.Equal(["/tv/", "/mnt/offline/"], roots.Select(r => r.Path));
        Assert.Equal(["4k", "kids"], tags.Select(t => t.Label));
        Assert.Equal(["/api/v3/qualityprofile", "/api/v3/rootfolder", "/api/v3/tag"], ApiPaths(handler));
    }

    // ---- Failures ----

    [Fact]
    public async Task A_Missing_Item_Says_So_Instead_Of_Blaming_The_Address() {
        var (factory, _) = Setup();

        var ex = await Assert.ThrowsAsync<ArrApiException>(() => factory.CreateSonarr(Server(ArrType.Sonarr)).GetSeriesByIdAsync(999));

        Assert.Equal(ArrErrorKind.NotFound, ex.Kind);
        Assert.Equal("That item no longer exists on the server.", ex.Message);
        Assert.DoesNotContain("address", ex.Message);
    }

    [Fact]
    public async Task A_Missing_Radarr_Item_Says_So_Too() {
        var (factory, _) = Setup();

        var ex = await Assert.ThrowsAsync<ArrApiException>(() => factory.CreateRadarr(Server(ArrType.Radarr)).GetMovieAsync(999));

        Assert.Equal("That item no longer exists on the server.", ex.Message);
    }

    [Fact]
    public async Task A_Rejected_Key_Surfaces_As_A_Friendly_Error_From_Any_Call() {
        var client = new ArrClientFactory(new HttpClient(FakeHandler.Json("{}", HttpStatusCode.Unauthorized)))
            .Create(Server(ArrType.Radarr));

        var ex = await Assert.ThrowsAsync<ArrApiException>(() => client.GetLibraryAsync());

        Assert.Equal(ArrErrorKind.Unauthorized, ex.Kind);
        Assert.DoesNotContain("KEY", ex.Message);
    }

    [Fact]
    public async Task Cancellation_Passes_Through_The_Client() {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = new FakeHandler((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct));
        var client = new ArrClientFactory(new HttpClient(handler)).Create(Server(ArrType.Sonarr));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetLibraryAsync(cts.Token));
    }

    // ---- Factory ----

    [Fact]
    public void Factory_Builds_The_Client_Matching_The_Server_Type() {
        var (factory, _) = Setup();

        Assert.IsType<SonarrClient>(factory.Create(Server(ArrType.Sonarr)));
        Assert.IsType<RadarrClient>(factory.Create(Server(ArrType.Radarr)));
    }

    [Fact]
    public void Factory_Refuses_To_Build_A_Client_For_The_Wrong_Type() {
        var (factory, _) = Setup();

        Assert.Throws<ArgumentException>(() => factory.CreateSonarr(Server(ArrType.Radarr)));
        Assert.Throws<ArgumentException>(() => factory.CreateRadarr(Server(ArrType.Sonarr)));
    }
}
