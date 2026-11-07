using System.Text.Json.Serialization;

namespace Datapoint.Terminal.Agent.Workspaces;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorkspaceSettings))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, InstructionSnapshot>))]
internal sealed partial class WorkspaceJsonContext : JsonSerializerContext;
