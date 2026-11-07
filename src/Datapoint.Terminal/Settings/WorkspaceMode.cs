using System.Text.Json.Serialization;
using Datapoint.Terminal.Agent;

namespace Datapoint.Terminal.Settings;

public sealed record WorkspaceMode
{
    public required string Folder { get; init; }

    [JsonConverter(typeof(ApprovalModeConverter))]
    public ApprovalMode Mode { get; init; }
}
