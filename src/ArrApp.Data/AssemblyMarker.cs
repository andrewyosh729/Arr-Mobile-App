using ArrApp.Core;

namespace ArrApp.Data;

/// <summary>Anchor type for the Data assembly (tests, DI scanning). Removed once real repositories exist.</summary>
public sealed class AssemblyMarker {
    internal static ArrType Default => ArrType.Sonarr;
}
