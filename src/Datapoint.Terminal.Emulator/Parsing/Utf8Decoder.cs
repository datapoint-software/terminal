using System.Runtime.InteropServices;
using System.Text;

namespace Datapoint.Terminal.Emulator.Parsing;

[StructLayout(LayoutKind.Auto)]
internal struct Utf8Decoder
{
    private int _remaining;
    private int _codePoint;
    private int _minimum;

    public readonly bool IsPending => _remaining > 0;

    public void Reset() => _remaining = 0;

    public Utf8Step Begin(byte value)
    {
        (_remaining, _codePoint, _minimum) = value switch
        {
            >= 0xC2 and <= 0xDF => (1, value & 0x1F, 0x80),
            >= 0xE0 and <= 0xEF => (2, value & 0x0F, 0x800),
            >= 0xF0 and <= 0xF4 => (3, value & 0x07, 0x10000),
            _ => (0, 0, 0),
        };
        return _remaining > 0 ? Utf8Step.NeedMore : Utf8Step.Invalid;
    }

    public Utf8Step Continue(byte value, out Rune character)
    {
        character = Rune.ReplacementChar;
        if (value is < 0x80 or > 0xBF)
        {
            _remaining = 0;
            return Utf8Step.Interrupted;
        }

        _codePoint = (_codePoint << 6) | (value & 0x3F);
        _remaining--;
        if (_remaining > 0)
        {
            return Utf8Step.NeedMore;
        }

        var isValid = _codePoint >= _minimum && Rune.IsValid(_codePoint);
        character = isValid ? new Rune(_codePoint) : Rune.ReplacementChar;
        return Utf8Step.Complete;
    }
}
