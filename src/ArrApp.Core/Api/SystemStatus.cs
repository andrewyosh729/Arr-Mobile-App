namespace ArrApp.Core.Api;

/// <summary>The few fields of <c>GET /api/v3/system/status</c> we use.</summary>
public sealed class SystemStatus {
    public string? AppName { get; set; }
    public string? InstanceName { get; set; }
    public string? Version { get; set; }
    public string? UrlBase { get; set; }
}
