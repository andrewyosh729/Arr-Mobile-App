using System.Text.Json.Serialization;

namespace ArrApp.Core.Api;

/// <summary>Source-generated JSON metadata: no reflection, safe for trimming/AOT on Android and iOS. Add new DTOs here.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(SystemStatus))]
public partial class ArrJsonContext : JsonSerializerContext;
