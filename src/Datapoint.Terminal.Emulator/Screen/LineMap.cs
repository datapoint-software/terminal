namespace Datapoint.Terminal.Emulator.Screen;

// Where each line of the primary screen went when it reflowed, numbered as the emulator numbers lines, so marks, selections
// and the agent's place in the output can follow their text.
public sealed class LineMap
{
    private readonly long _first;
    private readonly int[] _moved;
    private readonly long _last;

    internal LineMap(long first, int[] moved, long last)
    {
        _first = first;
        _moved = moved;
        _last = last;
    }

    public long Map(long line)
    {
        var index = line - _first;
        if (index < 0)
        {
            return line;
        }

        var mapped = index < _moved.Length ? _first + _moved[index] : _first + (_moved.Length == 0 ? 0 : _moved[^1] + 1) + (index - _moved.Length);
        return Math.Min(mapped, _last);
    }
}
