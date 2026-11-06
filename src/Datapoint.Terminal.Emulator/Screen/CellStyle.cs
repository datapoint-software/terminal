namespace Datapoint.Terminal.Emulator.Screen;

public readonly record struct CellStyle(
    TerminalColor Foreground,
    TerminalColor Background,
    TerminalColor UnderlineColor,
    CellAttributes Attributes,
    UnderlineStyle Underline)
{
    public static CellStyle Default { get; } = new(
        TerminalColor.Default,
        TerminalColor.Default,
        TerminalColor.Default,
        CellAttributes.None,
        UnderlineStyle.None);

    public bool Has(CellAttributes attribute) => (Attributes & attribute) == attribute;

    public CellStyle With(CellAttributes attribute) => this with { Attributes = Attributes | attribute };

    public CellStyle Without(CellAttributes attribute) => this with { Attributes = Attributes & ~attribute };
}
