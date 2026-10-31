using System.Net;
using ArrApp.Core.Api;

namespace ArrApp.Core.Tests;

public class ArrHttpClientTests {
    private const string StatusJson = """{"appName":"Sonarr","version":"4.0.9.2244","instanceName":"Sonarr"}""";

    private static ArrHttpClient Client(FakeHandler handler, string url = "http://host:8989", TimeSpan? timeout = null)
        => new(new HttpClient(handler), url, "KEY123", timeout);

    private static Task<SystemStatus> Get(ArrHttpClient c, CancellationToken ct = default)
        => c.GetAsync("system/status", ArrJsonContext.Default.SystemStatus, ct);

    [Fact]
    public async Task Sends_Api_Key_Header_And_Builds_Url_With_UrlBase() {
        var handler = FakeHandler.Json(StatusJson);

        var status = await Get(Client(handler, "https://media.example.com/sonarr/"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://media.example.com/sonarr/api/v3/system/status", request.RequestUri!.ToString());
        Assert.Equal("KEY123", Assert.Single(request.Headers.GetValues("X-Api-Key")));
        Assert.Equal("4.0.9.2244", status.Version);
        Assert.Equal("Sonarr", status.AppName);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ArrErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, ArrErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound, ArrErrorKind.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError, ArrErrorKind.ServerError)]
    [InlineData(HttpStatusCode.BadGateway, ArrErrorKind.ServerError)]
    [InlineData(HttpStatusCode.Redirect, ArrErrorKind.InvalidResponse)]
    public async Task Maps_Http_Status_To_Friendly_Error(HttpStatusCode status, ArrErrorKind expected) {
        var ex = await Assert.ThrowsAsync<ArrApiException>(() => Get(Client(FakeHandler.Json("{}", status))));

        Assert.Equal(expected, ex.Kind);
    }

    [Fact]
    public async Task Html_Instead_Of_Json_Is_InvalidResponse() {
        var handler = FakeHandler.Json("<html><body>Login</body></html>");

        var ex = await Assert.ThrowsAsync<ArrApiException>(() => Get(Client(handler)));

        Assert.Equal(ArrErrorKind.InvalidResponse, ex.Kind);
    }

    [Fact]
    public async Task Connection_Failure_Is_Unreachable() {
        var handler = FakeHandler.Throw(new HttpRequestException(HttpRequestError.ConnectionError, "refused"));

        var ex = await Assert.ThrowsAsync<ArrApiException>(() => Get(Client(handler)));

        Assert.Equal(ArrErrorKind.Unreachable, ex.Kind);
    }

    [Fact]
    public async Task Tls_Failure_Is_Reported_As_Tls() {
        var handler = FakeHandler.Throw(new HttpRequestException(HttpRequestError.SecureConnectionError, "bad cert"));

        var ex = await Assert.ThrowsAsync<ArrApiException>(() => Get(Client(handler)));

        Assert.Equal(ArrErrorKind.Tls, ex.Kind);
    }

    [Fact]
    public async Task Slow_Server_Times_Out() {
        var handler = new FakeHandler(async (_, ct) => {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return FakeHandler.Response(StatusJson);
        });

        var ex = await Assert.ThrowsAsync<ArrApiException>(() => Get(Client(handler, timeout: TimeSpan.FromMilliseconds(50))));

        Assert.Equal(ArrErrorKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task Caller_Cancellation_Is_Not_Reported_As_Timeout() {
        using var cts = new CancellationTokenSource();
        var handler = new FakeHandler(async (_, ct) => {
            await cts.CancelAsync();
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return FakeHandler.Response(StatusJson);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Get(Client(handler), cts.Token));
    }

    [Fact]
    public async Task Invalid_Url_Fails_Before_Any_Request() {
        var handler = FakeHandler.Json(StatusJson);

        var ex = await Assert.ThrowsAsync<ArrApiException>(() => Get(Client(handler, "ftp://nope")));

        Assert.Equal(ArrErrorKind.InvalidUrl, ex.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Error_Messages_Never_Contain_The_Api_Key() {
        var ex = await Assert.ThrowsAsync<ArrApiException>(() => Get(Client(FakeHandler.Json("{}", HttpStatusCode.Unauthorized))));

        Assert.DoesNotContain("KEY123", ex.Message);
    }
}
