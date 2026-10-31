using ArrApp.Core;

namespace ArrApp.Data;

public sealed class SettingsStore(IAppDatabase database) : ISettingsStore {
    public async Task<string?> GetAsync(string key) {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        var values = await db.QueryScalarsAsync<string>("SELECT Value FROM Setting WHERE Key = ?", key).ConfigureAwait(false);
        return values.FirstOrDefault();
    }

    public async Task SetAsync(string key, string value) {
        var db = await database.GetConnectionAsync().ConfigureAwait(false);
        await db.ExecuteAsync("INSERT OR REPLACE INTO Setting (Key, Value) VALUES (?, ?)", key, value).ConfigureAwait(false);
    }
}
