namespace Datapoint.Terminal.Emulator.Parsing;

public static class ControlCharacters
{
    public const byte Null = 0x00;
    public const byte Bell = 0x07;
    public const byte Backspace = 0x08;
    public const byte HorizontalTab = 0x09;
    public const byte LineFeed = 0x0A;
    public const byte VerticalTab = 0x0B;
    public const byte FormFeed = 0x0C;
    public const byte CarriageReturn = 0x0D;
    public const byte ShiftOut = 0x0E;
    public const byte ShiftIn = 0x0F;
    public const byte Cancel = 0x18;
    public const byte Substitute = 0x1A;
    public const byte Escape = 0x1B;
    public const byte Delete = 0x7F;
}
