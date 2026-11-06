namespace Datapoint.Terminal.Controls;

public sealed class TerminalResizedEventArgs(int columns, int rows) : EventArgs
{
    public int Columns { get; } = columns;

    public int Rows { get; } = rows;
}
