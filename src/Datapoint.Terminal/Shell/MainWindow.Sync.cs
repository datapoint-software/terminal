using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Agent.Workspaces.Sync;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    // Product decision 90: once per workspace, since moving it is the user's call and repeating the warning wouldn't change it.
    private void WarnAboutSyncFolder()
    {
        if (Workspace.IsReadOnly || _settings.HasWarnedSyncFolder(Workspace.Folder)
            || SyncFolders.Containing(Workspace.Folder, SyncFolders.ForThisMachine()) is not { } sync)
        {
            return;
        }

        _chat.AddNotice(Strings.Format(Strings.NoticeSyncFolder, Workspace.Name, sync.Service, WorkspaceFolders.Display(sync.Path)));
        _settings.WarnedSyncFolder(Workspace.Folder);
    }
}
