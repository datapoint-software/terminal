using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Agent.Tools;

/// <summary>
/// A tool described by an explicit JSON schema rather than by reflection, so it works with trimming and Native AOT and its
/// description to the model is exactly what we wrote.
/// </summary>
internal sealed class ToolFunction(string name, string description, string schema, Func<AIFunctionArguments, CancellationToken, Task<string>> handler) : AIFunction
{
    private readonly JsonElement _schema = JsonDocument.Parse(schema).RootElement.Clone();

    public override string Name => name;

    public override string Description => description;

    public override JsonElement JsonSchema => _schema;

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) =>
        await handler(arguments, cancellationToken).ConfigureAwait(false);
}
