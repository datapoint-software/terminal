namespace Datapoint.Terminal.Emulator.Screen;

public readonly record struct TerminalColor(ColorKind Kind, int Value)
{
    public static TerminalColor Default { get; } = new(ColorKind.Default, 0);

    public static TerminalColor Indexed(int index) => new(ColorKind.Indexed, index);

    public static TerminalColor Rgb(byte red, byte green, byte blue) =>
        new(ColorKind.Rgb, (red << 16) | (green << 8) | blue);
}
