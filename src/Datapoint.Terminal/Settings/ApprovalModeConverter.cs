using System.Text.Json;
using System.Text.Json.Serialization;
using Datapoint.Terminal.Agent;

namespace Datapoint.Terminal.Settings;

// Reads a mode it doesn't know, or Ignore permissions, which is never meant to be kept, as Ask: the safe way to be wrong.
public sealed class ApprovalModeConverter : JsonConverter<ApprovalMode>
{
    public override ApprovalMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
        && Enum.TryParse<ApprovalMode>(reader.GetString(), ignoreCase: true, out var mode)
        && Enum.IsDefined(mode)
        && mode != ApprovalMode.IgnorePermissions
            ? mode
            : ApprovalMode.Ask;

    public override void Write(Utf8JsonWriter writer, ApprovalMode value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}
