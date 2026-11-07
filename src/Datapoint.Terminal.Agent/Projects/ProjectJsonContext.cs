using System.Text.Json.Serialization;

namespace Datapoint.Terminal.Agent.Projects;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProjectSettings))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class ProjectJsonContext : JsonSerializerContext;
