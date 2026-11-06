namespace Datapoint.Terminal.Emulator.Parsing;

public readonly ref struct CsiSequence(
    byte privateMarker,
    ReadOnlySpan<byte> intermediates,
    byte final,
    CsiParameters parameters)
{
    public byte PrivateMarker { get; } = privateMarker;

    public ReadOnlySpan<byte> Intermediates { get; } = intermediates;

    public byte Final { get; } = final;

    public CsiParameters Parameters { get; } = parameters;
}
