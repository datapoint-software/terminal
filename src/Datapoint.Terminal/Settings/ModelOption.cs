namespace Datapoint.Terminal.Settings;

// One item of the model list: a model the app knows, or, with no model, Other… for one it doesn't.
public sealed record ModelOption(KnownModel? Model, string Label)
{
    public override string ToString() => Label;
}
