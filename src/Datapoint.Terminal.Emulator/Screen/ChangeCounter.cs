namespace Datapoint.Terminal.Emulator.Screen;

public sealed class ChangeCounter
{
    public long Current { get; private set; }

    public long Next()
    {
        Current++;
        return Current;
    }
}
