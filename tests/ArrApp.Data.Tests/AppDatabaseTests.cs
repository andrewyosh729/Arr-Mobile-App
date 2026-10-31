namespace ArrApp.Data.Tests;

public class AppDatabaseTests {
    [Fact]
    public async Task Fresh_Database_Is_Migrated_To_Latest_Version() {
        await using var t = new TestDb();

        var db = await t.Database.GetConnectionAsync();

        Assert.Equal(Migrations.All[^1].Version, await db.ExecuteScalarAsync<int>("PRAGMA user_version"));
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'ServerConnection'"));
    }

    [Fact]
    public async Task Reopening_Keeps_Data_And_Does_Not_Remigrate() {
        await using var t = new TestDb();
        await t.Repository.AddAsync(TestDb.Server("Home"));
        await t.Database.DisposeAsync();

        await using var second = new AppDatabase(t.Path);
        var reopened = new ServerConnectionRepository(second, new TestDb.ReversingProtector());

        Assert.Single(await reopened.GetAllAsync());
    }

    [Fact]
    public async Task Pending_Migrations_Run_In_Order_On_An_Existing_Database() {
        await using var t = new TestDb();
        await (await t.Database.GetConnectionAsync()).ExecuteScalarAsync<int>("SELECT 1");
        await t.Database.DisposeAsync();

        var next = Migrations.All[^1].Version + 1;
        var v2 = new Migration(next, "ALTER TABLE ServerConnection ADD COLUMN Note TEXT NULL");
        await using var upgraded = new AppDatabase(t.Path, [.. Migrations.All, v2]);
        var db = await upgraded.GetConnectionAsync();

        Assert.Equal(next, await db.ExecuteScalarAsync<int>("PRAGMA user_version"));
        await db.ExecuteAsync("UPDATE ServerConnection SET Note = 'ok'"); // column exists
    }

    [Fact]
    public async Task Database_Newer_Than_App_Is_Rejected() {
        await using var t = new TestDb();
        var v2 = new Migration(2, "CREATE TABLE Extra (Id INTEGER)");
        await using (var newer = new AppDatabase(t.Path, [Migrations.All[0], v2]))
            await newer.GetConnectionAsync();

        await using var older = new AppDatabase(t.Path, [Migrations.All[0]]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => older.GetConnectionAsync());
    }

    [Fact]
    public async Task Failed_Migration_Rolls_Back_And_Leaves_Version_Unchanged() {
        await using var t = new TestDb([new Migration(1, "CREATE TABLE A (Id INTEGER)", "THIS IS NOT SQL")]);

        await Assert.ThrowsAnyAsync<Exception>(() => t.Database.GetConnectionAsync());

        await using var probe = new AppDatabase(t.Path, []);
        var db = await probe.GetConnectionAsync();
        Assert.Equal(0, await db.ExecuteScalarAsync<int>("PRAGMA user_version"));
        Assert.Equal(0, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'A'"));
    }
}
