using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Agent.Projects.Ignore;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    // Called on the agent's thread before a command runs. Only local terminals are checked: the project's .terminalignore
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
            : Project.Folder;
        return IgnoredArguments.Find(command, folder, ProjectFolders.Home, path => TerminalIgnore.IsIgnored(path, Project.Folder, ProjectFolders.Home));
    }
}
