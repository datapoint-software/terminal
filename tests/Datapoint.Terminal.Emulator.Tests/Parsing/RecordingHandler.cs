using System.Globalization;
using System.Text;
using Datapoint.Terminal.Emulator.Parsing;

namespace Datapoint.Terminal.Emulator.Tests.Parsing;

public sealed class RecordingHandler : IParserHandler
{
    private readonly StringBuilder _printed = new();

    private readonly List<string> _events = [];

    public void Print(Rune character) => _printed.Append(character.ToString());

    public void PrintAscii(ReadOnlySpan<byte> text) => _printed.Append(Encoding.ASCII.GetString(text));

    public void Execute(byte control) => Record($"execute {control:X2}");

    public void EscDispatch(ReadOnlySpan<byte> intermediates, byte final) =>
        Record($"esc {Encoding.ASCII.GetString(intermediates)}{(char)final}");

    public void CsiDispatch(CsiSequence sequence) => Record($"csi {Describe(sequence)}");

    public void OscDispatch(ReadOnlySpan<byte> data) => Record($"osc {Encoding.UTF8.GetString(data)}");

    public void DcsHook(CsiSequence sequence) => Record($"dcs hook {Describe(sequence)}");

    public void DcsPut(byte data) => Record($"dcs put {(char)data}");

    public void DcsUnhook() => Record("dcs unhook");

    public void ApcDispatch(ReadOnlySpan<byte> data) => Record($"apc {Encoding.UTF8.GetString(data)}");

    public IReadOnlyList<string> Finish()
    {
        FlushPrinted();
        return _events;
    }

    private void Record(string description)
    {
        FlushPrinted();
        _events.Add(description);
    }

    private void FlushPrinted()
    {
        if (_printed.Length == 0)
        {
            return;
        }

        _events.Add($"print {_printed}");
        _printed.Clear();
    }

    private static string Describe(CsiSequence sequence)
    {
        var text = new StringBuilder();
        if (sequence.PrivateMarker != 0)
        {
            text.Append((char)sequence.PrivateMarker);
        }

        for (var index = 0; index < sequence.Parameters.Count; index++)
        {
            text.Append(sequence.Parameters[index].ToString(CultureInfo.InvariantCulture));
            text.Append(SeparatorAfter(sequence.Parameters, index));
        }

        text.Append(Encoding.ASCII.GetString(sequence.Intermediates));
        text.Append((char)sequence.Final);
        return text.ToString();
    }

    private static string SeparatorAfter(CsiParameters parameters, int index)
    {
        if (index == parameters.Count - 1)
        {
            return string.Empty;
        }

        return parameters.HasSubParameterAfter(index) ? ":" : ";";
    }
}
