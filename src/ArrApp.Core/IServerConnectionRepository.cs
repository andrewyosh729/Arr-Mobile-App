namespace ArrApp.Core;

public interface IServerConnectionRepository {
    /// <summary>All servers in display order.</summary>
    Task<IReadOnlyList<ServerConnection>> GetAllAsync();

    Task<ServerConnection?> GetAsync(int id);

    /// <summary>The default server, or null when none are saved.</summary>
    Task<ServerConnection?> GetDefaultAsync();

    /// <summary>Inserts and fills in <see cref="ServerConnection.Id"/>, <see cref="ServerConnection.SortOrder"/> and <see cref="ServerConnection.CreatedAt"/>. The first server saved becomes the default.</summary>
    Task AddAsync(ServerConnection connection);

    /// <summary>Updates editable fields (name, type, URL, key). Default flag and order are changed through their own methods.</summary>
    Task UpdateAsync(ServerConnection connection);

    /// <summary>Deletes a server; if it was the default, the first remaining server is promoted.</summary>
    Task DeleteAsync(int id);

    Task SetDefaultAsync(int id);

    /// <summary>Applies the given order (ids not listed keep their relative order after the listed ones).</summary>
    Task ReorderAsync(IReadOnlyList<int> orderedIds);

    Task MarkConnectedAsync(int id, string version, DateTimeOffset when);
}
