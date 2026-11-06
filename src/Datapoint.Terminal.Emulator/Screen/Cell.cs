using System.Text;

namespace Datapoint.Terminal.Emulator.Screen;

public struct Cell
{
    public const int WideContinuation = 0;

    public int CodePoint { get; set; }

    public string? Grapheme { get; set; }

    public byte Width { get; set; }

    public CellStyle Style { get; set; }

    public int HyperlinkId { get; set; }

    public readonly bool IsEmpty => CodePoint == 0 && Width == 1;

    public readonly bool IsWideContinuation => Width == WideContinuation;

    public static Cell Blank(CellStyle style) => new()
    {
        Width = 1,
        Style = style with { Attributes = CellAttributes.None, Underline = UnderlineStyle.None, Foreground = TerminalColor.Default },
    };

    public readonly string Text => Grapheme ?? (CodePoint == 0 ? string.Empty : new Rune(CodePoint).ToString());
}
