namespace Datapoint.Terminal.Controls;

public sealed class TerminalInputEventArgs(byte[] data) : EventArgs
{
    public byte[] Data { get; } = data;
}
