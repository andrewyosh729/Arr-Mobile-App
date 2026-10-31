using SQLite;

namespace ArrApp.Data;

public interface IAppDatabase {
    /// <summary>The shared connection, opened and migrated on first use.</summary>
    Task<SQLiteAsyncConnection> GetConnectionAsync();
}

/// <summary>
/// Owns the single SQLite connection for the app's lifetime and brings the schema up to date
/// using <c>PRAGMA user_version</c> as the schema version.
/// </summary>
public sealed class AppDatabase(string path, IReadOnlyList<Migration>? migrations = null) : IAppDatabase, IAsyncDisposable {
    private readonly IReadOnlyList<Migration> _migrations = migrations ?? Migrations.All;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    public async Task<SQLiteAsyncConnection> GetConnectionAsync() {
        if (_connection is not null)
            return _connection;

        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            if (_connection is not null)
                return _connection;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var connection = new SQLiteAsyncConnection(path);
            try {
                await MigrateAsync(connection).ConfigureAwait(false);
            } catch {
                await connection.CloseAsync().ConfigureAwait(false);
                throw;
            }

            _connection = connection;
            return connection;
        } finally {
            _gate.Release();
        }
    }

    private async Task MigrateAsync(SQLiteAsyncConnection connection) {
        var current = await connection.ExecuteScalarAsync<int>("PRAGMA user_version").ConfigureAwait(false);
        var latest = _migrations.Count == 0 ? 0 : _migrations[^1].Version;

        if (current > latest)
            throw new InvalidOperationException(
                $"Database schema version {current} is newer than this app understands ({latest}). Update the app.");

        foreach (var migration in _migrations.Where(m => m.Version > current).OrderBy(m => m.Version)) {
            await connection.RunInTransactionAsync(db => {
                foreach (var statement in migration.Statements)
                    db.Execute(statement);
                db.Execute($"PRAGMA user_version = {migration.Version}");
            }).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() {
        if (_connection is { } connection) {
            _connection = null;
            await connection.CloseAsync().ConfigureAwait(false);
        }
    }
}
