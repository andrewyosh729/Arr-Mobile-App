using System.Net;
using ArrApp.Core.Api;

namespace ArrApp.Core.Tests;

public class ArrConnectionTesterTests {
    private static Task<ConnectionTestResult> Test(FakeHandler handler, ArrType expected)
        => new ArrConnectionTester(new HttpClient(handler)).TestAsync(expected, "http://host:8989", "KEY");

    [Fact]
    public async Task Success_Reports_App_Name_And_Version() {
        var result = await Test(FakeHandler.Json("""{"appName":"Sonarr","version":"4.0.9.2244"}"""), ArrType.Sonarr);

        Assert.True(result.Success);
        Assert.Equal("Sonarr", result.AppName);
        Assert.Equal("4.0.9.2244", result.Version);
        Assert.Equal("Connected to Sonarr 4.0.9.2244.", result.Message);
    }

    [Fact]
    public async Task App_Name_Match_Ignores_Case() {
        var result = await Test(FakeHandler.Json("""{"appName":"radarr","version":"5.1.0"}"""), ArrType.Radarr);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Radarr_Saved_As_Sonarr_Is_Rejected() {
        var result = await Test(FakeHandler.Json("""{"appName":"Radarr","version":"5.1.0"}"""), ArrType.Sonarr);

        Assert.False(result.Success);
        Assert.Equal(ArrErrorKind.WrongApp, result.Error);
        Assert.Equal("Radarr", result.AppName);
        Assert.Contains("Radarr", result.Message);
        Assert.Contains("Sonarr", result.Message);
    }

    [Fact]
    public async Task Json_Without_App_Name_Is_Not_An_Arr() {
        var result = await Test(FakeHandler.Json("""{"hello":"world"}"""), ArrType.Sonarr);

        Assert.False(result.Success);
        Assert.Equal(ArrErrorKind.InvalidResponse, result.Error);
    }

    [Fact]
    public async Task Http_Failures_Become_Failed_Results_Not_Exceptions() {
        var result = await Test(FakeHandler.Json("{}", HttpStatusCode.Unauthorized), ArrType.Sonarr);

        Assert.False(result.Success);
        Assert.Equal(ArrErrorKind.Unauthorized, result.Error);
    }

    [Fact]
    public async Task Cancellation_Still_Propagates() {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = new FakeHandler((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ArrConnectionTester(new HttpClient(handler)).TestAsync(ArrType.Sonarr, "http://host", "KEY", cts.Token));
    }
}
