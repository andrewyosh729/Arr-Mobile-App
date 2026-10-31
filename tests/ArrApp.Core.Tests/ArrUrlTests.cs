using ArrApp.Core.Api;

namespace ArrApp.Core.Tests;

public class ArrUrlTests {
    [Theory]
    [InlineData("192.168.1.10:8989", "http://192.168.1.10:8989")]
    [InlineData("  http://nas.local:7878/  ", "http://nas.local:7878")]
    [InlineData("https://Media.Example.com", "https://media.example.com")]
    [InlineData("https://media.example.com/sonarr/", "https://media.example.com/sonarr")]
    [InlineData("https://media.example.com/sonarr/api/v3", "https://media.example.com/sonarr")]
    [InlineData("http://host:8989/api", "http://host:8989")]
    [InlineData("http://host:8989/?apikey=x#frag", "http://host:8989")]
    [InlineData("https://host:443", "https://host")]
    public void Normalizes(string input, string expected) {
        Assert.True(ArrUrl.TryNormalize(input, out var result, out var error), error);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("ftp://host")]
    [InlineData("http://")]
    [InlineData("not a url at all")]
    public void Rejects(string? input) {
        Assert.False(ArrUrl.TryNormalize(input, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }
}
