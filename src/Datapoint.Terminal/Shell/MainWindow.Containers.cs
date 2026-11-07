using Avalonia;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Controls;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions;
using Datapoint.Terminal.Sessions.Local;

namespace Datapoint.Terminal.Shell;

// Product.md, Containers: a container on this machine opens like a local tab, with docker or kubectl in place of the shell.
public sealed partial class MainWindow
{
    private async Task<TerminalTab?> OpenLocalContainerAsync(WorkspaceConnection connection, bool select)
    {
        var container = connection.Container!;
        var view = new TerminalView { Padding = new Thickness(12, 10), IsVisible = false };
        var tab = new TerminalTab(NextTabName(connection.Name), view, RequestClose) { IsRemote = true, ConnectionName = connection.Name };
        try
        {
            var session = await LocalSession.StartAsync(ContainerOptions(container), CancellationToken.None).ConfigureAwait(true);
            Attach(tab, Connect(tab, session), connection.Environment, select, files: null, container.Describe());
            return tab;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeConnectFailed, connection.Name, exception.Message));
            return null;
        }
    }

    // Through the user's login shell on macOS and Linux, so docker and kubectl are found on the PATH they set up there, which
    // apps started from the Dock don't get.
    private LocalSessionOptions ContainerOptions(WorkspaceContainer container)
    {
        var folder = Workspace.IsDefault ? new LocalSessionOptions().WorkingDirectory : Workspace.Folder;
        if (OperatingSystem.IsWindows())
        {
            var command = container.Command();
            return new LocalSessionOptions { Shell = command[0], Arguments = [.. command.Skip(1)], WorkingDirectory = folder, Login = false };
        }

        return new LocalSessionOptions { Shell = DefaultShell.Find(), Arguments = ["-l", "-c", "exec " + container.CommandLine()], WorkingDirectory = folder };
    }
}
