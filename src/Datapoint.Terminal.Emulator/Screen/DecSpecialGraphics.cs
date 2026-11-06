namespace Datapoint.Terminal.Emulator.Screen;

public static class DecSpecialGraphics
{
    private const string Glyphs = " ◆▒␉␌␍␊°±␤␋┘┐┌└┼⎺⎻─⎼⎽├┤┴┬│≤≥π≠£·";

    public static int Translate(int codePoint)
    {
        var index = codePoint - 0x5F;
        return index is >= 0 and < 32 ? Glyphs[index] : codePoint;
    }
}
