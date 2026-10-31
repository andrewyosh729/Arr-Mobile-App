using ArrApp.Core;
using SQLite;

namespace ArrApp.Data;

public sealed class ServerConnectionRepository(IAppDatabase database, IApiKeyProtector protector) : IServerConnectionRepository {
    [Table("ServerConnection")]
    private sealed class Row {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int ArrType { get; set; }
        public string BaseUrl { get; set; } = "";
        public string ProtectedApiKey { get; set; } = "";
        public bool IsDefault { get; set; }
        public int SortOrder { get; set; }
        public long CreatedAtUnixMs { get; set; }
        public long? LastConnectedAtUnixMs { get; set; }
        public string? LastKnownVersion { get; set; }
    }

    public async Task<IReadOnlyList<ServerConnection>> GetAllAsync() {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        var rows = await db.Table<Row>().OrderBy(r => r.SortOrder).ThenBy(r => r.Id).ToListAsync().ConfigureAwait(false);
        return rows.Select(ToModel).ToList();
    }

    public async Task<ServerConnection?> GetAsync(int id) {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        var row = await db.FindAsync<Row>(id).ConfigureAwait(false);
        return row is null ? null : ToModel(row);
    }

    public async Task<ServerConnection?> GetDefaultAsync() {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        var row = await db.Table<Row>().Where(r => r.IsDefault).FirstOrDefaultAsync().ConfigureAwait(false);
        return row is null ? null : ToModel(row);
    }

    public async Task AddAsync(ServerConnection connection) {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        var row = new Row();
        await db.RunInTransactionAsync(c => {
            var count = c.Table<Row>().Count();
            var maxOrder = count == 0 ? -1 : c.Table<Row>().OrderByDescending(r => r.SortOrder).First().SortOrder;

            row.Name = connection.Name;
            row.ArrType = (int)connection.ArrType;
            row.BaseUrl = connection.BaseUrl;
            row.ProtectedApiKey = protector.Protect(connection.ApiKey);
            row.IsDefault = count == 0;
            row.SortOrder = maxOrder + 1;
            row.CreatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            c.Insert(row);
        }).ConfigureAwait(false);

        connection.Id = row.Id;
        connection.IsDefault = row.IsDefault;
        connection.SortOrder = row.SortOrder;
        connection.CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAtUnixMs);
    }

    public async Task UpdateAsync(ServerConnection connection) {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        var protectedKey = protector.Protect(connection.ApiKey);
        await db.ExecuteAsync(
            "UPDATE ServerConnection SET Name = ?, ArrType = ?, BaseUrl = ?, ProtectedApiKey = ? WHERE Id = ?",
            connection.Name, (int)connection.ArrType, connection.BaseUrl, protectedKey, connection.Id).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id) {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.RunInTransactionAsync(c => {
            var wasDefault = c.Find<Row>(id)?.IsDefault ?? false;
            c.Delete<Row>(id);
            if (wasDefault) {
                var next = c.Table<Row>().OrderBy(r => r.SortOrder).ThenBy(r => r.Id).FirstOrDefault();
                if (next is not null)
                    c.Execute("UPDATE ServerConnection SET IsDefault = 1 WHERE Id = ?", next.Id);
            }
        }).ConfigureAwait(false);
    }

    public async Task SetDefaultAsync(int id) {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.RunInTransactionAsync(c => {
            if (c.Find<Row>(id) is null)
                return;
            c.Execute("UPDATE ServerConnection SET IsDefault = 0");
            c.Execute("UPDATE ServerConnection SET IsDefault = 1 WHERE Id = ?", id);
        }).ConfigureAwait(false);
    }

    public async Task ReorderAsync(IReadOnlyList<int> orderedIds) {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.RunInTransactionAsync(c => {
            var rest = c.Table<Row>().OrderBy(r => r.SortOrder).ThenBy(r => r.Id).ToList()
                .Select(r => r.Id).Where(id => !orderedIds.Contains(id));
            var order = 0;
            foreach (var id in orderedIds.Concat(rest))
                c.Execute("UPDATE ServerConnection SET SortOrder = ? WHERE Id = ?", order++, id);
        }).ConfigureAwait(false);
    }

    public async Task MarkConnectedAsync(int id, string version, DateTimeOffset when) {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.ExecuteAsync(
            "UPDATE ServerConnection SET LastKnownVersion = ?, LastConnectedAtUnixMs = ? WHERE Id = ?",
            version, when.ToUnixTimeMilliseconds(), id).ConfigureAwait(false);
    }

    /// <summary>
    /// A key that can no longer be decrypted (restored backup, new device, reinstalled key store) must not take the whole
    /// server list down. It comes back empty, so the server shows as unauthorized and the user re-enters the key.
    /// </summary>
    private string TryUnprotect(string protectedKey) {
        try {
            return protector.Unprotect(protectedKey);
        } catch (Exception) {
            return "";
        }
    }

    private ServerConnection ToModel(Row row) => new() {
        Id = row.Id,
        Name = row.Name,
        ArrType = (ArrType)row.ArrType,
        BaseUrl = row.BaseUrl,
        ApiKey = TryUnprotect(row.ProtectedApiKey),
        IsDefault = row.IsDefault,
        SortOrder = row.SortOrder,
        CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAtUnixMs),
        LastConnectedAt = row.LastConnectedAtUnixMs is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null,
        LastKnownVersion = row.LastKnownVersion,
    };
}
