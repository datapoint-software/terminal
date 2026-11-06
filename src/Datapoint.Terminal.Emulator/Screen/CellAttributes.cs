namespace Datapoint.Terminal.Emulator.Screen;

[Flags]
public enum CellAttributes : ushort
{
    None = 0,
    Bold = 1 << 0,
    Faint = 1 << 1,
    Italic = 1 << 2,
    Blink = 1 << 3,
    Inverse = 1 << 4,
    Invisible = 1 << 5,
    Strikethrough = 1 << 6,
    Overline = 1 << 7,
}
