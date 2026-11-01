using ArrApp.Core;
using ArrApp.Core.Models;
using ArrApp.UI.ViewModels;

namespace ArrApp.UI.Tests;

public class MediaCardViewModelTests {
    private static readonly ServerConnection Server = new() { BaseUrl = "http://nas:8989", ApiKey = "KEY", ArrType = ArrType.Sonarr };

    private static Series Show(string title = "Severance", int year = 2022, string? status = "continuing", bool monitored = true,
        int files = 15, int episodes = 19, bool poster = true) {
        var series = new Series {
            Id = 12, Title = title, Year = year, Status = status, Monitored = monitored,
            Statistics = new SeriesStatistics { EpisodeFileCount = files, EpisodeCount = episodes },
        };
        if (poster)
            series.Images.Add(new MediaImage { CoverType = "poster", Url = "/MediaCover/12/poster.jpg", RemoteUrl = "https://x.example/p.jpg" });
        return series;
    }

    [Fact]
    public void Shows_Title_Year_And_A_Detail_Line() {
        var card = new MediaCardViewModel(Show(), Server);

        Assert.Equal(12, card.Id);
        Assert.Equal("Severance", card.Title);
        Assert.Equal("2022", card.YearText);
        Assert.Equal("2022 · Continuing", card.Detail);
    }

    [Theory]
    [InlineData(2022, null, "2022")]
    [InlineData(0, "ended", "Ended")]
    [InlineData(0, null, "")]
    public void The_Detail_Line_Leaves_Out_What_Is_Unknown(int year, string? status, string expected) {
        var card = new MediaCardViewModel(Show(year: year, status: status), Server);

        Assert.Equal(expected, card.Detail);
        Assert.Equal(year > 0 ? year.ToString() : "", card.YearText);
    }

    [Theory]
    [InlineData("severance", "S")]
    [InlineData("Écoute", "É")]
    [InlineData("", "?")]
    public void The_Placeholder_Letter_Is_The_Uppercased_First_Letter(string title, string expected)
        => Assert.Equal(expected, new MediaCardViewModel(Show(title: title), Server).Initial);

    [Fact]
    public void The_Poster_Request_Uses_The_Server_And_Key_With_The_Public_Url_As_Fallback() {
        var card = new MediaCardViewModel(Show(), Server);

        var candidates = card.PosterRequest.Candidates();

        Assert.Equal(2, candidates.Count);
        Assert.Equal("http://nas:8989/MediaCover/12/poster.jpg", candidates[0].Url);
        Assert.Equal("KEY", candidates[0].ApiKey);
        Assert.False(candidates[1].SendApiKey);
    }

    [Fact]
    public void An_Item_Without_A_Poster_Has_An_Empty_Request_So_The_Placeholder_Shows() {
        var card = new MediaCardViewModel(Show(poster: false), Server);

        Assert.Empty(card.PosterRequest.Candidates());
    }

    [Fact]
    public void A_Series_Card_Exposes_Badge_And_Progress_For_The_View() {
        var card = new MediaCardViewModel(Show(files: 4, episodes: 12), Server);

        Assert.True(card.HasBadge);
        Assert.Equal("4/12 eps", card.BadgeText);
        Assert.True(card.BadgeIsWarning);
        Assert.False(card.BadgeIsSuccess);
        Assert.False(card.BadgeIsDanger);
        Assert.True(card.HasProgress);
        Assert.Equal(4.0 / 12, card.Progress, 6);
    }

    [Fact]
    public void A_Movie_Card_Has_A_Badge_But_No_Progress() {
        var movie = new Movie { Id = 7, Title = "Dune: Part Two", Year = 2024, HasFile = true, Monitored = true, Status = "released" };
        var server = new ServerConnection { BaseUrl = "http://nas:7878", ApiKey = "KEY", ArrType = ArrType.Radarr };

        var card = new MediaCardViewModel(movie, server);

        Assert.Equal("Downloaded", card.BadgeText);
        Assert.True(card.BadgeIsSuccess);
        Assert.False(card.HasProgress);
        Assert.Equal(0, card.Progress);
        Assert.Equal("2024 · Released", card.Detail);
    }

    [Fact]
    public void The_Monitored_Flag_Passes_Through() {
        Assert.True(new MediaCardViewModel(Show(monitored: true), Server).IsMonitored);
        Assert.False(new MediaCardViewModel(Show(monitored: false), Server).IsMonitored);
    }

    [Fact]
    public void A_Card_For_A_Barely_Populated_Item_Does_Not_Fail() {
        var card = new MediaCardViewModel(new Series { Title = "x" }, Server);

        Assert.Equal("No episodes", card.BadgeText);
        Assert.False(card.HasProgress);
        Assert.Equal("", card.Detail);
        Assert.Empty(card.PosterRequest.Candidates());
    }
}
