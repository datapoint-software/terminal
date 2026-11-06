using System.Text;

namespace Datapoint.Terminal.Emulator.Parsing;

public interface IParserHandler
{
    void Print(Rune character);

    void PrintAscii(ReadOnlySpan<byte> text);

    void Execute(byte control);

    void EscDispatch(ReadOnlySpan<byte> intermediates, byte final);

    void CsiDispatch(CsiSequence sequence);

    void OscDispatch(ReadOnlySpan<byte> data);

    void DcsHook(CsiSequence sequence);

    void DcsPut(byte data);

    void DcsUnhook();

    void ApcDispatch(ReadOnlySpan<byte> data);
}
