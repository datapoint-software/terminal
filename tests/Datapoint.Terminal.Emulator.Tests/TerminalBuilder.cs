namespace Datapoint.Terminal.Emulator.Tests;

public static class TerminalBuilder
{
    public static Terminal Small(ITerminalHost? host = null) => new(10, 4, host, maxScrollback: 100);

    public static Terminal With(string output, int columns = 10, int rows = 4)
    {
        var terminal = new Terminal(columns, rows, maxScrollback: 100);
        terminal.Feed(output);
        return terminal;
    }
}
