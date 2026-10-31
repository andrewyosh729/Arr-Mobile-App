namespace ArrApp.Data.Tests;

public class SettingsStoreTests {
    [Fact]
    public async Task Missing_Key_Reads_As_Null() {
        await using var t = new TestDb();

        Assert.Null(await new SettingsStore(t.Database).GetAsync("nope"));
    }

    [Fact]
    public async Task Set_Then_Get_Round_Trips_And_Overwrites() {
        await using var t = new TestDb();
        var store = new SettingsStore(t.Database);

        await store.SetAsync("LastActiveServerId", "3");
        Assert.Equal("3", await store.GetAsync("LastActiveServerId"));

        await store.SetAsync("LastActiveServerId", "7");
        Assert.Equal("7", await store.GetAsync("LastActiveServerId"));
    }

    [Fact]
    public async Task Keys_Are_Independent() {
        await using var t = new TestDb();
        var store = new SettingsStore(t.Database);

        await store.SetAsync("a", "1");
        await store.SetAsync("b", "2");

        Assert.Equal("1", await store.GetAsync("a"));
        Assert.Equal("2", await store.GetAsync("b"));
    }

    [Fact]
    public async Task Values_Survive_Reopening_The_Database() {
        await using var t = new TestDb();
        await new SettingsStore(t.Database).SetAsync("k", "v");
        await t.Database.DisposeAsync();

        await using var reopened = new AppDatabase(t.Path);

        Assert.Equal("v", await new SettingsStore(reopened).GetAsync("k"));
    }

    [Fact]
    public async Task An_Existing_Version_1_Database_Gains_The_Settings_Table_On_Upgrade() {
        await using var t = new TestDb([Migrations.All[0]]);
        await t.Repository.AddAsync(TestDb.Server("Home"));
        await t.Database.DisposeAsync();

        await using var upgraded = new AppDatabase(t.Path); // latest migrations
        var store = new SettingsStore(upgraded);
        await store.SetAsync("k", "v");

        Assert.Equal("v", await store.GetAsync("k"));
        Assert.Single(await new ServerConnectionRepository(upgraded, new TestDb.ReversingProtector()).GetAllAsync());
    }
}
