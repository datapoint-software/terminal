namespace Datapoint.Terminal.Emulator.Unicode;

public static class CharacterWidth
{
    public static int Of(int codePoint)
    {
        if (codePoint is >= 0x20 and < 0x7F)
        {
            return 1;
        }

        var index = FindRange(codePoint);
        return index < 0 ? 1 : WidthTable.Widths[index];
    }

    private static int FindRange(int codePoint)
    {
        var low = 0;
        var high = WidthTable.Starts.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (codePoint < WidthTable.Starts[middle])
            {
                high = middle - 1;
                continue;
            }

            if (codePoint > WidthTable.Ends[middle])
            {
                low = middle + 1;
                continue;
            }

            return middle;
        }

        return -1;
    }
}
