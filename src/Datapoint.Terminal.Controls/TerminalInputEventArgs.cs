namespace Datapoint.Terminal.Controls;

/// <summary>Bytes the user typed or pasted, to send to the shell.</summary>
public sealed class TerminalInputEventArgs(byte[] data) : EventArgs
{
    public byte[] Data { get; } = data;
}
