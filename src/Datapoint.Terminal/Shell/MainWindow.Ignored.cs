using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Agent.Workspaces.Ignore;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    // Called on the agent's thread before a command runs. Only local terminals are checked: the workspace's .terminalignore
    // describes files on this machine, not on the hosts it connects to.
    private string? FindIgnoredFile(AgentTerminal terminal, string command)
    {
        var state = terminal.Terminal.GetState();
        if (terminal.IsRemote || state.Session is not null)
        {
            return null;
        }

        var folder = state.WorkingDirectory is { Length: > 0 } reported && Path.IsPathFullyQualified(reported)
            ? reported
            : Workspace.Folder;
        return IgnoredArguments.Find(command, folder, WorkspaceFolders.Home, path => TerminalIgnore.IsIgnored(path, Workspace.Folder, WorkspaceFolders.Home));
    }
}
