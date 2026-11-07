using EmulatorTerminal = Datapoint.Terminal.Emulator.Terminal;

namespace Datapoint.Terminal.Sessions.Recording;

// Replays a recording into an emulator. Going back starts again from the beginning, which is quick: only output is fed, nothing
// is drawn until the caller asks.
public sealed class RecordingPlayer
{
    private readonly SessionRecording _recording;
    private int _next;

    public RecordingPlayer(SessionRecording recording)
    {
        ArgumentNullException.ThrowIfNull(recording);
        _recording = recording;
        Terminal = new EmulatorTerminal(recording.Columns, recording.Rows);
    }

    public EmulatorTerminal Terminal { get; private set; }

    public double Position { get; private set; }

    public bool IsAtEnd => _next >= _recording.Events.Count;

    // Raised when going back replaced Terminal.
    public event EventHandler? TerminalReplaced;

    public void Seek(double position)
    {
        position = Math.Clamp(position, 0, _recording.Duration);
        if (position < Position)
        {
            Terminal = new EmulatorTerminal(_recording.Columns, _recording.Rows);
            _next = 0;
            TerminalReplaced?.Invoke(this, EventArgs.Empty);
        }

        while (_next < _recording.Events.Count && _recording.Events[_next].Time <= position)
        {
            Apply(_recording.Events[_next++]);
        }

        Position = position;
    }

    private void Apply(RecordingEvent item)
    {
        switch (item.Type)
        {
            case "o":
                Terminal.Feed(item.Data);
                break;
            case "r" when SessionRecording.ParseSize(item.Data) is { } size:
                Terminal.Resize(size.Columns, size.Rows);
                break;
        }
    }
}
