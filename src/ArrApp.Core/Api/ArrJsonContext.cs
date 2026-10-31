using System.Text.Json.Serialization;
using ArrApp.Core.Models;

namespace ArrApp.Core.Api;

/// <summary>
/// Source-generated JSON metadata: no reflection, safe for trimming/AOT on Android and iOS. Add new DTOs here.
/// Unknown fields in a server's JSON are ignored, so newer Arr versions adding fields don't break parsing.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(SystemStatus))]
[JsonSerializable(typeof(Series))]
[JsonSerializable(typeof(List<Series>))]
[JsonSerializable(typeof(Movie))]
[JsonSerializable(typeof(List<Movie>))]
[JsonSerializable(typeof(List<Episode>))]
[JsonSerializable(typeof(List<QualityProfile>))]
[JsonSerializable(typeof(List<RootFolder>))]
[JsonSerializable(typeof(List<Tag>))]
[JsonSerializable(typeof(List<Release>))]
[JsonSerializable(typeof(QueuePage))]
public partial class ArrJsonContext : JsonSerializerContext;
