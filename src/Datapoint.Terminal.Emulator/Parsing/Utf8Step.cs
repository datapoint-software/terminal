namespace Datapoint.Terminal.Emulator.Parsing;

internal enum Utf8Step
{
    NeedMore,
    Complete,
    Invalid,
    Interrupted,
}
