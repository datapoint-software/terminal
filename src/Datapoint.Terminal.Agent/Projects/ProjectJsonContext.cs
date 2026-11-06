using System.Text.Json.Serialization;

namespace Datapoint.Terminal.Agent.Projects;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProjectSettings))]
internal sealed partial class ProjectJsonContext : JsonSerializerContext;
