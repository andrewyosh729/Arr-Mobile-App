using ArrApp.Core;

namespace ArrApp.Data.Tests;

public class ServerConnectionRepositoryTests {
    [Fact]
    public async Task First_Server_Becomes_Default_And_Gets_Id_Order_And_Timestamp() {
        await using var t = new TestDb();
        var home = TestDb.Server("Home");

        await t.Repository.AddAsync(home);

        Assert.True(home.Id > 0);
        Assert.True(home.IsDefault);
        Assert.Equal(0, home.SortOrder);
        Assert.True(home.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(home.Id, (await t.Repository.GetDefaultAsync())!.Id);
    }

    [Fact]
    public async Task Later_Servers_Are_Not_Default_And_Append_To_The_End() {
        await using var t = new TestDb();
        await t.Repository.AddAsync(TestDb.Server("A"));
        var b = TestDb.Server("B", ArrType.Radarr);

        await t.Repository.AddAsync(b);

        Assert.False(b.IsDefault);
        Assert.Equal(1, b.SortOrder);
        Assert.Equal(["A", "B"], (await t.Repository.GetAllAsync()).Select(s => s.Name));
    }

    [Fact]
    public async Task Api_Key_Round_Trips_But_Is_Never_Stored_In_Plain_Text() {
        await using var t = new TestDb();
        var server = TestDb.Server("Home", key: "abc123");
        await t.Repository.AddAsync(server);

        var loaded = await t.Repository.GetAsync(server.Id);
        var raw = await (await t.Database.GetConnectionAsync())
            .ExecuteScalarAsync<string>("SELECT ProtectedApiKey FROM ServerConnection");

        Assert.Equal("abc123", loaded!.ApiKey);
        Assert.DoesNotContain("abc123", raw);
    }

    [Fact]
    public async Task Update_Changes_Editable_Fields_Only() {
        await using var t = new TestDb();
        var server = TestDb.Server("Home");
        await t.Repository.AddAsync(server);

        server.Name = "Renamed";
        server.ArrType = ArrType.Radarr;
        server.BaseUrl = "https://new.example/radarr";
        server.ApiKey = "new-key";
        server.IsDefault = false; // must be ignored
        await t.Repository.UpdateAsync(server);

        var loaded = (await t.Repository.GetAsync(server.Id))!;
        Assert.Equal("Renamed", loaded.Name);
        Assert.Equal(ArrType.Radarr, loaded.ArrType);
        Assert.Equal("https://new.example/radarr", loaded.BaseUrl);
        Assert.Equal("new-key", loaded.ApiKey);
        Assert.True(loaded.IsDefault);
    }

    [Fact]
    public async Task Deleting_The_Default_Promotes_The_First_Remaining() {
        await using var t = new TestDb();
        var a = TestDb.Server("A");
        var b = TestDb.Server("B");
        var c = TestDb.Server("C");
        foreach (var s in new[] { a, b, c })
            await t.Repository.AddAsync(s);

        await t.Repository.DeleteAsync(a.Id);

        Assert.Equal(b.Id, (await t.Repository.GetDefaultAsync())!.Id);
        Assert.Equal(1, (await t.Repository.GetAllAsync()).Count(s => s.IsDefault));
    }

    [Fact]
    public async Task Deleting_The_Last_Server_Leaves_No_Default() {
        await using var t = new TestDb();
        var a = TestDb.Server("A");
        await t.Repository.AddAsync(a);

        await t.Repository.DeleteAsync(a.Id);

        Assert.Empty(await t.Repository.GetAllAsync());
        Assert.Null(await t.Repository.GetDefaultAsync());
    }

    [Fact]
    public async Task SetDefault_Leaves_Exactly_One_Default() {
        await using var t = new TestDb();
        var a = TestDb.Server("A");
        var b = TestDb.Server("B");
        await t.Repository.AddAsync(a);
        await t.Repository.AddAsync(b);

        await t.Repository.SetDefaultAsync(b.Id);

        var all = await t.Repository.GetAllAsync();
        Assert.Equal(b.Id, Assert.Single(all, s => s.IsDefault).Id);
    }

    [Fact]
    public async Task Reorder_Applies_Order_And_Keeps_Unlisted_Servers_After() {
        await using var t = new TestDb();
        var a = TestDb.Server("A");
        var b = TestDb.Server("B");
        var c = TestDb.Server("C");
        foreach (var s in new[] { a, b, c })
            await t.Repository.AddAsync(s);

        await t.Repository.ReorderAsync([c.Id, a.Id]);

        Assert.Equal(["C", "A", "B"], (await t.Repository.GetAllAsync()).Select(s => s.Name));
    }

    [Fact]
    public async Task MarkConnected_Records_Version_And_Time() {
        await using var t = new TestDb();
        var a = TestDb.Server("A");
        await t.Repository.AddAsync(a);
        var when = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

        await t.Repository.MarkConnectedAsync(a.Id, "4.0.9.2244", when);

        var loaded = (await t.Repository.GetAsync(a.Id))!;
        Assert.Equal("4.0.9.2244", loaded.LastKnownVersion);
        Assert.Equal(when, loaded.LastConnectedAt);
    }
}
