namespace ArrApp.Data;

/// <summary>One forward-only schema step. Raw SQL on purpose: a migration must never change meaning when entity classes evolve.</summary>
public sealed record Migration(int Version, params string[] Statements);

public static class Migrations {
    /// <summary>Ordered by version, starting at 1. Never edit a shipped migration; append a new one.</summary>
    public static readonly IReadOnlyList<Migration> All = [
        new(1,
            """
            CREATE TABLE ServerConnection (
                Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                Name TEXT NOT NULL,
                ArrType INTEGER NOT NULL,
                BaseUrl TEXT NOT NULL,
                ProtectedApiKey TEXT NOT NULL,
                IsDefault INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAtUnixMs INTEGER NOT NULL,
                LastConnectedAtUnixMs INTEGER NULL,
                LastKnownVersion TEXT NULL
            )
            """),
    ];
}
