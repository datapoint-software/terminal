using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Hosts;
using Datapoint.Terminal.Agent.Memory;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Agent.Workspaces.Ignore;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Shell;

// Product decisions 29 and 102: memory is personal, kept in the application's data, changed by the agent in view in the chat
// and by the user in Settings › Memory.
public sealed partial class MainWindow
{
    // A read-only window would race the window that holds the workspace's lock, so only the global memory is written from it.
    private AgentMemory CreateMemory() => new(GlobalMemoryPath, Workspace.IsReadOnly ? null : WorkspaceMemoryPath);

    // Product decision 89: documentation for each connection, in the workspace's hosts/ folder.
    private HostDocsFolder CreateHostDocs() => new(
        Path.Combine(Workspace.Folder, "hosts"),
        path => TerminalIgnore.IsIgnored(path, Workspace.Folder, WorkspaceFolders.Home),
        CanWrite: !Workspace.IsReadOnly);

    // Product decision 102: memory is viewed and changed in Settings › Memory.
    public MemoryPlaces MemoryPlaces => new(GlobalMemoryPath, Workspace.IsDefault ? null : Workspace.Name, WorkspaceMemoryPath, CanWriteWorkspace: !Workspace.IsReadOnly);

    private void AddMemoryItems(NativeMenu file) => file.Add(Item(Strings.MenuMemory, null, OpenMemory));

    private void OpenMemory() => _windows.OpenSettings(SettingsWindow.MemoryPageIndex);

    private void OnMemoryEvent(object? sender, AgentEventArgs e)
    {
        if (e.Path is not { } path)
        {
            return;
        }

        if (e.Kind == AgentEventKind.MemoryChanged)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _windows.ReloadMemory();
                _chat.AddNotice(e.Text, Strings.MemoryOpen, OpenMemory);
            });
        }
        else if (e.Kind == AgentEventKind.HostDocsChanged)
        {
            Dispatcher.UIThread.Post(() =>
            {
                RefreshHostLogs();
                _chat.AddNotice(e.Text, Strings.HostDocsOpen, () => OpenWorkspaceFile(path));
            });
        }
    }
}
