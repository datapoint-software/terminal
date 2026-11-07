using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Emulator;

public sealed class LinesMovedEventArgs(LineMap map) : EventArgs
{
    public LineMap Map { get; } = map;
}
