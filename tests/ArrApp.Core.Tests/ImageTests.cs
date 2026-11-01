using System.Net;
using ArrApp.Core.Images;
using ArrApp.Core.Models;

namespace ArrApp.Core.Tests;

public class ImageRequestTests {
    [Theory]
    [InlineData("http://nas:8989", "/MediaCover/12/poster.jpg?lastWrite=638", "http://nas:8989/MediaCover/12/poster.jpg?lastWrite=638")]
    [InlineData("http://nas:8989/", "/MediaCover/12/poster.jpg", "http://nas:8989/MediaCover/12/poster.jpg")]
    [InlineData("https://host/sonarr", "/MediaCover/12/poster.jpg", "https://host/sonarr/MediaCover/12/poster.jpg")]
    // With a URL base set, the Arr already includes it in the image path: it must not be added twice.
    [InlineData("https://host/sonarr", "/sonarr/MediaCover/12/poster.jpg", "https://host/sonarr/MediaCover/12/poster.jpg")]
    [InlineData("https://host/sonarr/api/v3", "/sonarr/MediaCover/12/poster.jpg", "https://host/sonarr/MediaCover/12/poster.jpg")]
    [InlineData("http://nas:8989", "MediaCover/12/poster.jpg", "http://nas:8989/MediaCover/12/poster.jpg")]
    public void Server_Image_Paths_Are_Joined_To_The_Server_Address(string baseUrl, string path, string expected)
        => Assert.Equal(expected, ImageRequest.ResolveServerUrl(baseUrl, path));

    [Fact]
    public void The_Server_Is_Tried_First_With_The_Key_And_The_Public_Url_Second_Without_It() {
        var request = new ImageRequest("http://nas:8989", "SECRET", "/MediaCover/1/poster.jpg", "https://image.tmdb.org/p.jpg");

        var candidates = request.Candidates();

        Assert.Equal(2, candidates.Count);
        Assert.Equal("http://nas:8989/MediaCover/1/poster.jpg", candidates[0].Url);
        Assert.Equal("SECRET", candidates[0].ApiKey);
        Assert.Equal("https://image.tmdb.org/p.jpg", candidates[1].Url);
        Assert.False(candidates[1].SendApiKey); // never leak the key to a third party
    }

    [Fact]
    public void An_Absolute_Url_In_The_Servers_Json_Never_Gets_The_Key() {
        var request = new ImageRequest("http://nas:8989", "SECRET", "https://elsewhere.example/p.jpg", null);

        var candidate = Assert.Single(request.Candidates());

        Assert.Equal("https://elsewhere.example/p.jpg", candidate.Url);
        Assert.False(candidate.SendApiKey);
    }

    [Fact]
    public void Missing_Pieces_Just_Mean_Fewer_Candidates() {
        Assert.Empty(new ImageRequest(null, null, null, null).Candidates());
        Assert.Empty(new ImageRequest("http://nas", "K", "  ", "not a url").Candidates());
        Assert.Single(new ImageRequest(null, null, null, "https://x.example/p.jpg").Candidates());
        Assert.Single(new ImageRequest("http://nas", "K", "/MediaCover/1/p.jpg", null).Candidates());
    }

    [Fact]
    public void A_Duplicate_Remote_Url_Is_Not_Listed_Twice() {
        var request = new ImageRequest(null, null, "https://x.example/p.jpg", "https://x.example/p.jpg");

        Assert.Single(request.Candidates());
    }

    [Fact]
    public void Describing_A_Request_Never_Prints_The_Api_Key() {
        var request = new ImageRequest("http://nas", "SECRET", "/MediaCover/1/p.jpg", null);

        Assert.DoesNotContain("SECRET", request.ToString());
        Assert.DoesNotContain("SECRET", request.Candidates()[0].ToString());
    }

    [Fact]
    public void Requests_Can_Be_Built_From_A_Server_And_An_Image() {
        var server = new ServerConnection { BaseUrl = "http://nas:8989", ApiKey = "K" };
        var image = new MediaImage { CoverType = "poster", Url = "/MediaCover/1/poster.jpg", RemoteUrl = "https://x.example/p.jpg" };

        Assert.Equal(2, ImageRequest.For(server, image).Candidates().Count);
    }
}

public class DiskImageCacheTests : IDisposable {
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arrapp-img-" + Guid.NewGuid().ToString("N"));

    public void Dispose() {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private static byte[] Bytes(int length, byte fill = 1) => Enumerable.Repeat(fill, length).ToArray();

    [Fact]
    public async Task Put_Then_Get_Round_Trips_And_A_Miss_Is_Null() {
        var cache = new DiskImageCache(_dir);

        Assert.Null(await cache.GetAsync("http://a/1.jpg"));
        await cache.PutAsync("http://a/1.jpg", [1, 2, 3]);

        Assert.Equal([1, 2, 3], await cache.GetAsync("http://a/1.jpg"));
        Assert.Null(await cache.GetAsync("http://a/2.jpg"));
    }

    [Fact]
    public async Task Different_Urls_Do_Not_Collide_And_Overwriting_Works() {
        var cache = new DiskImageCache(_dir);

        await cache.PutAsync("http://a/1.jpg", [1]);
        await cache.PutAsync("http://a/2.jpg", [2]);
        await cache.PutAsync("http://a/1.jpg", [9, 9]);

        Assert.Equal([9, 9], await cache.GetAsync("http://a/1.jpg"));
        Assert.Equal([2], await cache.GetAsync("http://a/2.jpg"));
    }

    [Fact]
    public async Task The_Least_Recently_Used_Image_Is_Evicted_First_When_Over_Budget() {
        var cache = new DiskImageCache(_dir, maxBytes: 300);
        await cache.PutAsync("old", Bytes(100));
        await Task.Delay(30);
        await cache.PutAsync("middle", Bytes(100));
        await Task.Delay(30);
        await cache.PutAsync("recent", Bytes(100));
        await Task.Delay(30);
        await cache.GetAsync("old"); // touching "old" makes "middle" the least recently used

        await cache.PutAsync("newest", Bytes(100)); // 400 > 300: trims down to 270

        Assert.Null(await cache.GetAsync("middle"));
        Assert.Null(await cache.GetAsync("recent")); // 400 -> 270 needs two evictions
        Assert.NotNull(await cache.GetAsync("old"));
        Assert.NotNull(await cache.GetAsync("newest"));
        Assert.True(cache.SizeBytes <= 300);
    }

    [Fact]
    public async Task Size_Reflects_Contents_And_Clear_Empties_The_Cache() {
        var cache = new DiskImageCache(_dir);
        Assert.Equal(0, cache.SizeBytes);

        await cache.PutAsync("a", Bytes(50));
        await cache.PutAsync("b", Bytes(70));
        Assert.Equal(120, cache.SizeBytes);

        await cache.ClearAsync();

        Assert.Equal(0, cache.SizeBytes);
        Assert.Null(await cache.GetAsync("a"));
    }

    [Fact]
    public async Task Concurrent_Writes_To_The_Same_Url_Leave_A_Complete_File() {
        var cache = new DiskImageCache(_dir);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.PutAsync("same", Bytes(5000, 7))));

        Assert.Equal(Bytes(5000, 7), await cache.GetAsync("same"));
    }
}

public class ImageLoaderTests : IDisposable {
    private const string ServerUrl = "http://nas:8989/MediaCover/1/poster.jpg";
    private const string RemoteUrl = "https://image.tmdb.org/poster.jpg";
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arrapp-img-" + Guid.NewGuid().ToString("N"));

    public void Dispose() {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private static ImageRequest Request() => new("http://nas:8989", "SECRET", "/MediaCover/1/poster.jpg", RemoteUrl);

    private static HttpResponseMessage Image(byte[] bytes, string contentType = "image/jpeg") {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return response;
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    private (ImageLoader Loader, FakeHandler Server, FakeHandler Remote, DiskImageCache Cache) Create(
        Func<HttpRequestMessage, HttpResponseMessage>? server = null, Func<HttpRequestMessage, HttpResponseMessage>? remote = null,
        TimeProvider? time = null, long maxCacheBytes = 200_000_000) {
        var serverHandler = new FakeHandler((r, _) => Task.FromResult((server ?? (_ => Image(Jpeg)))(r)));
        var remoteHandler = new FakeHandler((r, _) => Task.FromResult((remote ?? (_ => Image(Jpeg)))(r)));
        var cache = new DiskImageCache(_dir, maxCacheBytes);
        return (new ImageLoader(new HttpClient(serverHandler), new HttpClient(remoteHandler), cache, time), serverHandler, remoteHandler, cache);
    }

    [Fact]
    public async Task Loads_From_The_Server_With_The_Api_Key_And_Caches_It() {
        var (loader, server, remote, cache) = Create();

        var bytes = await loader.LoadAsync(Request());

        Assert.Equal(Jpeg, bytes);
        var sent = Assert.Single(server.Requests);
        Assert.Equal(ServerUrl, sent.RequestUri!.ToString());
        Assert.Equal("SECRET", Assert.Single(sent.Headers.GetValues("X-Api-Key")));
        Assert.Empty(remote.Requests);
        Assert.Equal(Jpeg, await cache.GetAsync(ServerUrl));
    }

    [Fact]
    public async Task A_Cached_Image_Needs_No_Network_At_All() {
        var (loader, server, remote, _) = Create();
        await loader.LoadAsync(Request());

        var again = await loader.LoadAsync(Request());

        Assert.Equal(Jpeg, again);
        Assert.Single(server.Requests);
        Assert.Empty(remote.Requests);
    }

    [Fact]
    public async Task Falls_Back_To_The_Public_Url_Without_The_Api_Key() {
        var (loader, server, remote, cache) = Create(server: _ => Status(HttpStatusCode.NotFound));

        var bytes = await loader.LoadAsync(Request());

        Assert.Equal(Jpeg, bytes);
        Assert.Single(server.Requests);
        var sent = Assert.Single(remote.Requests);
        Assert.Equal(RemoteUrl, sent.RequestUri!.ToString());
        Assert.False(sent.Headers.Contains("X-Api-Key")); // the key must never reach a third party
        Assert.Equal(Jpeg, await cache.GetAsync(RemoteUrl));
    }

    [Fact]
    public async Task A_Server_That_Is_Down_Falls_Back_Too() {
        var (loader, _, _, _) = Create(server: _ => throw new HttpRequestException("refused"));

        Assert.Equal(Jpeg, await loader.LoadAsync(Request()));
    }

    [Fact]
    public async Task Once_The_Fallback_Is_Cached_The_Next_Load_Needs_No_Network() {
        var (loader, server, remote, _) = Create(server: _ => Status(HttpStatusCode.NotFound));
        await loader.LoadAsync(Request());

        await loader.LoadAsync(Request());

        Assert.Single(server.Requests);
        Assert.Single(remote.Requests);
    }

    [Fact]
    public async Task A_Non_Image_Response_Is_Not_Accepted_And_Not_Cached() {
        var (loader, _, _, cache) = Create(
            server: _ => Image([1, 2], "text/html"),
            remote: _ => Status(HttpStatusCode.Forbidden));

        Assert.Null(await loader.LoadAsync(Request()));
        Assert.Equal(0, cache.SizeBytes);
    }

    [Fact]
    public async Task An_Oversized_Image_Is_Rejected() {
        var huge = new byte[ImageLoader.MaxImageBytes + 1];
        var (loader, _, _, cache) = Create(server: _ => Image(huge), remote: _ => Image(huge));

        Assert.Null(await loader.LoadAsync(Request()));
        Assert.Equal(0, cache.SizeBytes);
    }

    [Fact]
    public async Task A_Redirect_From_The_Server_Is_Not_Followed_Or_Treated_As_An_Image() {
        var (loader, _, remote, _) = Create(
            server: _ => Status(HttpStatusCode.Redirect),
            remote: _ => Status(HttpStatusCode.NotFound));

        Assert.Null(await loader.LoadAsync(Request()));
        Assert.Single(remote.Requests); // only the explicit public fallback, never the redirect target
    }

    [Fact]
    public async Task Simultaneous_Requests_For_The_Same_Image_Share_One_Download() {
        var release = new TaskCompletionSource();
        var calls = 0;
        var serverHandler = new FakeHandler(async (_, _) => {
            Interlocked.Increment(ref calls);
            await release.Task;
            return Image(Jpeg);
        });
        var loader = new ImageLoader(new HttpClient(serverHandler), new HttpClient(FakeHandler.Json("x")), new DiskImageCache(_dir));

        var loads = Enumerable.Range(0, 8).Select(_ => loader.LoadAsync(Request())).ToList();
        await Task.Delay(100);
        release.SetResult();
        var results = await Task.WhenAll(loads);

        Assert.Equal(1, calls);
        Assert.All(results, r => Assert.Equal(Jpeg, r));
    }

    [Fact]
    public async Task One_Caller_Giving_Up_Does_Not_Cancel_The_Download_For_The_Others() {
        var release = new TaskCompletionSource();
        var serverHandler = new FakeHandler(async (_, _) => {
            await release.Task;
            return Image(Jpeg);
        });
        var loader = new ImageLoader(new HttpClient(serverHandler), new HttpClient(FakeHandler.Json("x")), new DiskImageCache(_dir));
        using var impatient = new CancellationTokenSource();

        var gone = loader.LoadAsync(Request(), impatient.Token);
        var patient = loader.LoadAsync(Request());
        await Task.Delay(50);
        await impatient.CancelAsync();
        release.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gone);
        Assert.Equal(Jpeg, await patient);
    }

    [Fact]
    public async Task A_Failed_Image_Is_Not_Retried_Until_The_Negative_Cache_Expires() {
        var clock = new MutableTime();
        var (loader, server, remote, _) = Create(
            server: _ => Status(HttpStatusCode.NotFound), remote: _ => Status(HttpStatusCode.NotFound), time: clock);

        Assert.Null(await loader.LoadAsync(Request()));
        Assert.Null(await loader.LoadAsync(Request())); // scrolled past again: no new requests
        Assert.Single(server.Requests);
        Assert.Single(remote.Requests);

        clock.Advance(ImageLoader.NegativeCacheDuration + TimeSpan.FromSeconds(1));
        Assert.Null(await loader.LoadAsync(Request()));

        Assert.Equal(2, server.Requests.Count);
    }

    [Fact]
    public async Task Nothing_To_Load_Means_Null_Without_Any_Request() {
        var (loader, server, remote, _) = Create();

        Assert.Null(await loader.LoadAsync(new ImageRequest(null, null, null, null)));
        Assert.Empty(server.Requests);
        Assert.Empty(remote.Requests);
    }

    [Fact]
    public async Task A_Slow_Server_Times_Out_And_The_Fallback_Is_Used() {
        var serverHandler = new FakeHandler(async (_, ct) => {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return Image(Jpeg);
        });
        var loader = new ImageLoader(new HttpClient(serverHandler), new HttpClient(FakeHandler.Json("x")), new DiskImageCache(_dir),
            timeout: TimeSpan.FromMilliseconds(100));

        // The public fake answers with JSON, which is not an image, so the result is null, but it must come back
        // quickly instead of waiting out the slow server.
        var started = DateTime.UtcNow;
        Assert.Null(await loader.LoadAsync(Request()));
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    private sealed class MutableTime : TimeProvider {
        private DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}

public class ImageLoaderPlatformFailureTests : IDisposable {
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "arrapp-img-" + Guid.NewGuid().ToString("N"));

    public void Dispose() {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task A_Platform_Specific_Failure_Gives_No_Image_Instead_Of_An_Exception(Type exceptionType) {
        var thrown = (Exception)Activator.CreateInstance(exceptionType, "unexpected end of stream")!;
        var loader = new ImageLoader(new HttpClient(FakeHandler.Throw(thrown)), new HttpClient(FakeHandler.Throw(thrown)), new DiskImageCache(_dir));

        var bytes = await loader.LoadAsync(new ImageRequest("http://nas", "K", "/MediaCover/1/p.jpg", "https://x.example/p.jpg"));

        Assert.Null(bytes);
    }
}
