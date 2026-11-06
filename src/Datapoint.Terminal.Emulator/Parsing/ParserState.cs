namespace Datapoint.Terminal.Emulator.Parsing;

public enum ParserState
{
    Ground,
    Escape,
    EscapeIntermediate,
    CsiEntry,
    CsiParameter,
    CsiIntermediate,
    CsiIgnore,
    DcsEntry,
    DcsParameter,
    DcsIntermediate,
    DcsPassthrough,
    DcsIgnore,
    OscString,
    SosPmApcString,
}
