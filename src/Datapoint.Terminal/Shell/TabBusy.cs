using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Why closing a tab would cut something off, or null when it wouldn't. Without shell integration a local shell can't tell
// whether a command is running, so only what's known is reported; a remote tab always counts, since closing it ends the
// connection and whatever still runs over it.
internal static class TabBusy
{
    public static string? Reason(TerminalState? state, bool isRemote, bool isAgentActing) =>
        ForTyping(state, isAgentActing) ?? (isRemote ? Strings.BusyRemote : null);

    // Why a command typed into the terminal now wouldn't reach the shell; null when it's at a prompt, or can't tell.
    public static string? ForTyping(TerminalState? state, bool isAgentActing) =>
        isAgentActing ? Strings.BusyAgent
        : state is { IsAlternateScreen: true } ? Strings.BusyFullScreen
        : state is { HasShellIntegration: true, IsAtPrompt: false } ? Strings.BusyCommand
        : null;
}
