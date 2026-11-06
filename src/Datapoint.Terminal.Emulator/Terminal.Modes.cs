using Datapoint.Terminal.Emulator.Parsing;
using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Emulator;

public sealed partial class Terminal
{
    private void SetAnsiModes(CsiParameters parameters, bool enabled)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            switch (parameters[index])
            {
                case 4:
                    Modes.Insert = enabled;
                    break;
                case 20:
                    Modes.LineFeedNewLine = enabled;
                    break;
            }
        }
    }

    private void SetPrivateModes(CsiParameters parameters, bool enabled)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            SetPrivateMode(parameters[index], enabled);
        }
    }

    private void SetPrivateMode(int mode, bool enabled)
    {
        if (SetCursorMode(mode, enabled) || SetInputMode(mode, enabled))
        {
            return;
        }

        switch (mode)
        {
            case 47 or 1047:
                SwitchScreen(enabled, saveCursor: false, clear: mode == 1047);
                break;
            case 1048:
                SaveOrRestoreCursor(enabled);
                break;
            case 1049:
                SwitchScreen(enabled, saveCursor: true, clear: true);
                break;
            case 2026:
                Modes.SynchronizedOutput = enabled;
                break;
            case 2027:
                Modes.GraphemeClustering = enabled;
                break;
        }
    }

    private bool SetCursorMode(int mode, bool enabled)
    {
        switch (mode)
        {
            case 1:
                Modes.ApplicationCursorKeys = enabled;
                return true;
            case 5:
                Modes.ReverseVideo = enabled;
                TouchAllRows();
                return true;
            case 6:
                Modes.Origin = enabled;
                MoveCursorTo(0, 0);
                return true;
            case 7:
                Modes.AutoWrap = enabled;
                return true;
            case 12:
                Modes.CursorBlinking = enabled;
                return true;
            case 25:
                Modes.CursorVisible = enabled;
                return true;
            default:
                return false;
        }
    }

    private bool SetInputMode(int mode, bool enabled)
    {
        switch (mode)
        {
            case 1000 or 1002 or 1003:
                Modes.MouseTracking = enabled ? MouseTrackingFor(mode) : MouseTracking.None;
                return true;
            case 1004:
                Modes.FocusEvents = enabled;
                return true;
            case 1006:
                Modes.SgrMouse = enabled;
                return true;
            case 2004:
                Modes.BracketedPaste = enabled;
                return true;
            default:
                return false;
        }
    }

    private static MouseTracking MouseTrackingFor(int mode) => mode switch
    {
        1000 => MouseTracking.Clicks,
        1002 => MouseTracking.ClicksAndDrags,
        _ => MouseTracking.AllMotion,
    };

    private void SaveOrRestoreCursor(bool save)
    {
        if (save)
        {
            SaveCursor();
            return;
        }

        RestoreCursor();
    }

    private void SwitchScreen(bool toAlternate, bool saveCursor, bool clear)
    {
        if (toAlternate == IsAlternateScreen)
        {
            return;
        }

        if (toAlternate)
        {
            EnterAlternateScreen(saveCursor, clear);
            return;
        }

        Screen = Primary;
        if (saveCursor)
        {
            RestoreCursor();
        }

        TouchAllRows();
    }

    private void EnterAlternateScreen(bool saveCursor, bool clear)
    {
        if (saveCursor)
        {
            SaveCursor();
        }

        Screen = Alternate;
        if (clear)
        {
            Alternate.EraseRows(0, Rows - 1, Cursor.Style);
        }

        TouchAllRows();
    }

    private void TouchAllRows()
    {
        for (var row = 0; row < Rows; row++)
        {
            Screen.Touch(row);
        }
    }

    private int PrivateModeState(int mode) => mode switch
    {
        1 => StateOf(Modes.ApplicationCursorKeys),
        5 => StateOf(Modes.ReverseVideo),
        6 => StateOf(Modes.Origin),
        7 => StateOf(Modes.AutoWrap),
        12 => StateOf(Modes.CursorBlinking),
        25 => StateOf(Modes.CursorVisible),
        _ => InputModeState(mode),
    };

    private int InputModeState(int mode) => mode switch
    {
        1000 => StateOf(Modes.MouseTracking == MouseTracking.Clicks),
        1002 => StateOf(Modes.MouseTracking == MouseTracking.ClicksAndDrags),
        1003 => StateOf(Modes.MouseTracking == MouseTracking.AllMotion),
        1004 => StateOf(Modes.FocusEvents),
        1006 => StateOf(Modes.SgrMouse),
        47 or 1047 or 1049 => StateOf(IsAlternateScreen),
        2004 => StateOf(Modes.BracketedPaste),
        2026 => StateOf(Modes.SynchronizedOutput),
        2027 => StateOf(Modes.GraphemeClustering),
        _ => 0,
    };

    private static int StateOf(bool enabled) => enabled ? 1 : 2;
}
