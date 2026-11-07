using System.Text.Json.Serialization;

namespace Datapoint.Terminal.Agent.Web;

[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
internal sealed partial class WebJsonContext : JsonSerializerContext;
