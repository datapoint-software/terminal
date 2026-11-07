namespace Datapoint.Terminal.Agent;

public sealed partial class TerminalAgent
{
    private volatile int _mode;
    private volatile int _modeBeforeIgnoring;

    public event EventHandler? ModeChanged;

    public ApprovalMode Mode
    {
        get => (ApprovalMode)_mode;
        set
        {
            if (_mode == (int)value)
            {
                return;
            }

            if (value == ApprovalMode.IgnorePermissions)
            {
                _modeBeforeIgnoring = _mode;
            }

            _mode = (int)value;
            ModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // What to remember for the workspace: Ignore permissions lasts only until the conversation ends or the application closes,
    // so the mode before it is kept instead.
    public ApprovalMode LastingMode => Mode == ApprovalMode.IgnorePermissions ? (ApprovalMode)_modeBeforeIgnoring : Mode;

    private void EndIgnoringPermissions() => Mode = LastingMode;
}
