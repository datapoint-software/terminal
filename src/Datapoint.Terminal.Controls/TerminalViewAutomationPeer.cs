using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace Datapoint.Terminal.Controls;

// Avalonia has no text pattern for custom controls yet, so line-by-line reading isn't available (engineering.md, Accessibility).
public sealed class TerminalViewAutomationPeer(TerminalView owner) : ControlAutomationPeer(owner), IValueProvider
{
    public bool IsReadOnly => true;

    public string? Value => ((TerminalView)Owner).GetVisibleText();

    public void SetValue(string? value) => throw new InvalidOperationException("The terminal's text can't be set.");

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

    protected override string GetNameCore() => "Terminal";

    protected override string GetLocalizedControlTypeCore() => "terminal";
}
