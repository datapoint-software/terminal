namespace Datapoint.Terminal.Emulator.Screen;

public sealed class TerminalModes
{
    public bool ApplicationCursorKeys { get; set; }

    public bool ApplicationKeypad { get; set; }

    public bool Origin { get; set; }

    public bool AutoWrap { get; set; } = true;

    public bool CursorVisible { get; set; } = true;

    public bool CursorBlinking { get; set; }

    public bool Insert { get; set; }

    public bool LineFeedNewLine { get; set; }

    public bool ReverseVideo { get; set; }

    public bool BracketedPaste { get; set; }

    public bool FocusEvents { get; set; }

    public MouseTracking MouseTracking { get; set; }

    public bool SgrMouse { get; set; }

    public bool SynchronizedOutput { get; set; }

    public bool GraphemeClustering { get; set; }

    public CursorShape CursorShape { get; set; }

    public void Reset()
    {
        ApplicationCursorKeys = false;
        ApplicationKeypad = false;
        Origin = false;
        AutoWrap = true;
        CursorVisible = true;
        Insert = false;
        LineFeedNewLine = false;
        ReverseVideo = false;
        BracketedPaste = false;
        FocusEvents = false;
        MouseTracking = MouseTracking.None;
        SgrMouse = false;
        SynchronizedOutput = false;
        CursorShape = CursorShape.Default;
    }
}
