using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Datapoint.Terminal.Emulator.Parsing;

public sealed class Parser
{
    public const int MaxParameters = 32;
    public const int MaxIntermediates = 2;
    public const int MaxParameterValue = 65535;
    public const int DefaultMaxStringLength = 64 * 1024;

    private static readonly SearchValues<byte> s_printableAscii = SearchValues.Create(
        Enumerable.Range(0x20, 0x7F - 0x20).Select(value => (byte)value).ToArray());

    private readonly IParserHandler _handler;
    private readonly int _maxStringLength;
    private readonly int[] _parameters = new int[MaxParameters];
    private readonly bool[] _subParameterAfter = new bool[MaxParameters];
    private readonly byte[] _intermediates = new byte[MaxIntermediates];
    private byte[] _string = new byte[256];
    private int _parameterCount;
    private bool _parametersOverflowed;
    private int _intermediateCount;
    private bool _intermediatesOverflowed;
    private byte _privateMarker;
    private int _stringLength;
    private bool _stringOverflowed;
    private bool _stringIsApc;
    private Utf8Decoder _utf8;

    public Parser(IParserHandler handler, int maxStringLength = DefaultMaxStringLength)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStringLength);
        _handler = handler;
        _maxStringLength = maxStringLength;
    }

    public ParserState State { get; private set; } = ParserState.Ground;

    public void Advance(ReadOnlySpan<byte> data)
    {
        while (!data.IsEmpty)
        {
            var printable = State == ParserState.Ground && !_utf8.IsPending
                ? CountPrintableAscii(data)
                : 0;
            if (printable > 0)
            {
                _handler.PrintAscii(data[..printable]);
                data = data[printable..];
                continue;
            }

            Advance(data[0]);
            data = data[1..];
        }
    }

    public void Advance(byte value)
    {
        if (TryHandleAnywhere(value))
        {
            return;
        }

        Dispatch(value);
    }

    private static int CountPrintableAscii(ReadOnlySpan<byte> data)
    {
        var end = data.IndexOfAnyExcept(s_printableAscii);
        return end < 0 ? data.Length : end;
    }

    [SuppressMessage("Maintainability", "CA1502", Justification = "One switch over the states, as the Williams state machine describes them.")]
    private void Dispatch(byte value)
    {
        switch (State)
        {
            case ParserState.Ground:
                Ground(value);
                break;
            case ParserState.Escape:
                Escape(value);
                break;
            case ParserState.EscapeIntermediate:
                EscapeIntermediate(value);
                break;
            case ParserState.CsiEntry:
                CsiEntry(value);
                break;
            case ParserState.CsiParameter:
                CsiParameter(value);
                break;
            case ParserState.CsiIntermediate:
                CsiIntermediate(value);
                break;
            case ParserState.CsiIgnore:
                CsiIgnore(value);
                break;
            case ParserState.DcsEntry:
                DcsEntry(value);
                break;
            case ParserState.DcsParameter:
                DcsParameter(value);
                break;
            case ParserState.DcsIntermediate:
                DcsIntermediate(value);
                break;
            case ParserState.DcsPassthrough:
                DcsPassthrough(value);
                break;
            case ParserState.DcsIgnore:
                break;
            case ParserState.OscString:
                OscString(value);
                break;
            case ParserState.SosPmApcString:
                SosPmApcString(value);
                break;
            default:
                throw new UnreachableException();
        }
    }

    private bool TryHandleAnywhere(byte value)
    {
        if (value is not (ControlCharacters.Cancel or ControlCharacters.Substitute or ControlCharacters.Escape))
        {
            return false;
        }

        FlushPendingUtf8();
        var isEscape = value == ControlCharacters.Escape;
        LeaveCurrentState(dispatchString: isEscape);
        if (isEscape)
        {
            ClearSequence();
            State = ParserState.Escape;
            return true;
        }

        if (State is not (ParserState.OscString or ParserState.SosPmApcString or ParserState.DcsPassthrough or ParserState.DcsIgnore))
        {
            _handler.Execute(value);
        }

        State = ParserState.Ground;
        return true;
    }

    private void LeaveCurrentState(bool dispatchString)
    {
        switch (State)
        {
            case ParserState.OscString when dispatchString && !_stringOverflowed:
                _handler.OscDispatch(_string.AsSpan(0, _stringLength));
                break;
            case ParserState.SosPmApcString when dispatchString && _stringIsApc && !_stringOverflowed:
                _handler.ApcDispatch(_string.AsSpan(0, _stringLength));
                break;
            case ParserState.DcsPassthrough:
                _handler.DcsUnhook();
                break;
        }
    }

    private void Ground(byte value)
    {
        if (_utf8.IsPending)
        {
            ContinueUtf8(value);
            return;
        }

        switch (value)
        {
            case < 0x20:
                _handler.Execute(value);
                break;
            case < ControlCharacters.Delete:
                _handler.PrintAscii([value]);
                break;
            case ControlCharacters.Delete:
                break;
            default:
                BeginUtf8(value);
                break;
        }
    }

    private void BeginUtf8(byte value)
    {
        if (_utf8.Begin(value) != Utf8Step.Invalid)
        {
            return;
        }

        _handler.Print(Rune.ReplacementChar);
    }

    private void ContinueUtf8(byte value)
    {
        var step = _utf8.Continue(value, out var character);
        if (step == Utf8Step.Complete)
        {
            _handler.Print(character);
            return;
        }

        if (step != Utf8Step.Interrupted)
        {
            return;
        }

        _handler.Print(Rune.ReplacementChar);
        Ground(value);
    }

    private void FlushPendingUtf8()
    {
        if (!_utf8.IsPending)
        {
            return;
        }

        _utf8.Reset();
        _handler.Print(Rune.ReplacementChar);
    }

    private void Escape(byte value)
    {
        switch (value)
        {
            case < 0x20:
                _handler.Execute(value);
                break;
            case <= 0x2F:
                Collect(value);
                State = ParserState.EscapeIntermediate;
                break;
            case (byte)'[':
                State = ParserState.CsiEntry;
                break;
            case (byte)']':
                BeginString(isApc: false, ParserState.OscString);
                break;
            case (byte)'P':
                State = ParserState.DcsEntry;
                break;
            case (byte)'X' or (byte)'^' or (byte)'_':
                BeginString(isApc: value == (byte)'_', ParserState.SosPmApcString);
                break;
            default:
                EscapeIntermediate(value);
                break;
        }
    }

    private void EscapeIntermediate(byte value)
    {
        switch (value)
        {
            case < 0x20:
                _handler.Execute(value);
                break;
            case <= 0x2F:
                Collect(value);
                break;
            case < ControlCharacters.Delete:
                DispatchEscape(value);
                break;
        }
    }

    private void DispatchEscape(byte final)
    {
        if (!_intermediatesOverflowed)
        {
            _handler.EscDispatch(_intermediates.AsSpan(0, _intermediateCount), final);
        }

        State = ParserState.Ground;
    }

    private void CsiEntry(byte value)
    {
        if (value is >= 0x3C and <= 0x3F)
        {
            _privateMarker = value;
            State = ParserState.CsiParameter;
            return;
        }

        CsiParameter(value);
    }

    private void CsiParameter(byte value)
    {
        switch (value)
        {
            case < 0x20:
                _handler.Execute(value);
                break;
            case <= 0x2F:
                Collect(value);
                State = ParserState.CsiIntermediate;
                break;
            case <= 0x3B:
                AddParameterByte(value);
                State = ParserState.CsiParameter;
                break;
            case <= 0x3F:
                State = ParserState.CsiIgnore;
                break;
            case < ControlCharacters.Delete:
                DispatchCsi(value);
                break;
        }
    }

    private void CsiIntermediate(byte value)
    {
        switch (value)
        {
            case < 0x20:
                _handler.Execute(value);
                break;
            case <= 0x2F:
                Collect(value);
                break;
            case <= 0x3F:
                State = ParserState.CsiIgnore;
                break;
            case < ControlCharacters.Delete:
                DispatchCsi(value);
                break;
        }
    }

    private void CsiIgnore(byte value)
    {
        switch (value)
        {
            case < 0x20:
                _handler.Execute(value);
                break;
            case >= 0x40 and < ControlCharacters.Delete:
                State = ParserState.Ground;
                break;
        }
    }

    private void DispatchCsi(byte final)
    {
        State = ParserState.Ground;
        if (_intermediatesOverflowed || _parametersOverflowed)
        {
            return;
        }

        _handler.CsiDispatch(CurrentSequence(final));
    }

    private void DcsEntry(byte value)
    {
        if (value is >= 0x3C and <= 0x3F)
        {
            _privateMarker = value;
            State = ParserState.DcsParameter;
            return;
        }

        DcsParameter(value);
    }

    private void DcsParameter(byte value)
    {
        switch (value)
        {
            case < 0x20:
                break;
            case <= 0x2F:
                Collect(value);
                State = ParserState.DcsIntermediate;
                break;
            case <= 0x3B:
                AddParameterByte(value);
                State = ParserState.DcsParameter;
                break;
            case <= 0x3F:
                State = ParserState.DcsIgnore;
                break;
            case < ControlCharacters.Delete:
                HookDcs(value);
                break;
        }
    }

    private void DcsIntermediate(byte value)
    {
        switch (value)
        {
            case < 0x20:
                break;
            case <= 0x2F:
                Collect(value);
                break;
            case <= 0x3F:
                State = ParserState.DcsIgnore;
                break;
            case < ControlCharacters.Delete:
                HookDcs(value);
                break;
        }
    }

    private void HookDcs(byte final)
    {
        if (_intermediatesOverflowed || _parametersOverflowed)
        {
            State = ParserState.DcsIgnore;
            return;
        }

        State = ParserState.DcsPassthrough;
        _handler.DcsHook(CurrentSequence(final));
    }

    private void DcsPassthrough(byte value)
    {
        if (value == ControlCharacters.Delete)
        {
            return;
        }

        _handler.DcsPut(value);
    }

    private void OscString(byte value)
    {
        if (value == ControlCharacters.Bell)
        {
            LeaveCurrentState(dispatchString: true);
            State = ParserState.Ground;
            return;
        }

        AppendPrintable(value);
    }

    private void SosPmApcString(byte value) => AppendPrintable(value);

    private void AppendPrintable(byte value)
    {
        if (value < 0x20)
        {
            return;
        }

        AppendString(value);
    }

    private void BeginString(bool isApc, ParserState state)
    {
        _stringLength = 0;
        _stringOverflowed = false;
        _stringIsApc = isApc;
        State = state;
    }

    private void AppendString(byte value)
    {
        if (_stringLength >= _maxStringLength)
        {
            _stringOverflowed = true;
            return;
        }

        if (_stringLength == _string.Length)
        {
            Array.Resize(ref _string, Math.Min(_string.Length * 2, _maxStringLength));
        }

        _string[_stringLength] = value;
        _stringLength++;
    }

    private void Collect(byte value)
    {
        if (_intermediateCount == MaxIntermediates)
        {
            _intermediatesOverflowed = true;
            return;
        }

        _intermediates[_intermediateCount] = value;
        _intermediateCount++;
    }

    private void AddParameterByte(byte value)
    {
        if (_parameterCount == 0)
        {
            _parameterCount = 1;
        }

        if (value is (byte)';' or (byte)':')
        {
            StartNextParameter(isSubParameter: value == (byte)':');
            return;
        }

        var index = _parameterCount - 1;
        _parameters[index] = Math.Min((_parameters[index] * 10) + (value - '0'), MaxParameterValue);
    }

    private void StartNextParameter(bool isSubParameter)
    {
        if (_parameterCount == MaxParameters)
        {
            _parametersOverflowed = true;
            return;
        }

        _subParameterAfter[_parameterCount - 1] = isSubParameter;
        _parameters[_parameterCount] = 0;
        _subParameterAfter[_parameterCount] = false;
        _parameterCount++;
    }

    private CsiSequence CurrentSequence(byte final) =>
        new(
            _privateMarker,
            _intermediates.AsSpan(0, _intermediateCount),
            final,
            new CsiParameters(_parameters.AsSpan(0, _parameterCount), _subParameterAfter.AsSpan(0, _parameterCount)));

    private void ClearSequence()
    {
        _parameterCount = 0;
        _parameters[0] = 0;
        _subParameterAfter[0] = false;
        _parametersOverflowed = false;
        _intermediateCount = 0;
        _intermediatesOverflowed = false;
        _privateMarker = 0;
    }
}
