namespace ArrApp.Core;

/// <summary>Small persistent key/value store for app state such as the last active server.</summary>
public interface ISettingsStore {
    /// <summary>The stored value, or null when the key has never been set.</summary>
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);
}
