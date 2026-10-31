using ArrApp.Core;

namespace ArrApp.Data.Tests;

/// <summary>Temp-file database that cleans up after itself.</summary>
public sealed class TestDb : IAsyncDisposable {
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arrapp-test-" + Guid.NewGuid().ToString("N") + ".db");
    public AppDatabase Database { get; }
    public ServerConnectionRepository Repository { get; }

    public TestDb(IReadOnlyList<Migration>? migrations = null) {
        Database = new AppDatabase(Path, migrations);
        Repository = new ServerConnectionRepository(Database, new ReversingProtector());
    }

    public async ValueTask DisposeAsync() {
        await Database.DisposeAsync();
        foreach (var file in new[] { Path, Path + "-wal", Path + "-shm" })
            if (File.Exists(file))
                File.Delete(file);
    }

    /// <summary>Reversible fake so tests can prove the stored value is not the plain key.</summary>
    public sealed class ReversingProtector : IApiKeyProtector {
        public string Protect(string plainText) => "enc:" + new string(plainText.Reverse().ToArray());
        public string Unprotect(string protectedText) => new string(protectedText["enc:".Length..].Reverse().ToArray());
    }

    public static ServerConnection Server(string name, ArrType type = ArrType.Sonarr, string key = "secret-key") => new() {
        Name = name,
        ArrType = type,
        BaseUrl = $"http://{name.ToLowerInvariant()}.local:8989",
        ApiKey = key,
    };
}
