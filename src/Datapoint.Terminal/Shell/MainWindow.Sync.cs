using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Agent.Projects.Sync;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    // Product decision 90: once per project, since moving it is the user's call and repeating the warning wouldn't change it.
    private void WarnAboutSyncFolder()
    {
        if (Project.IsReadOnly || _settings.HasWarnedSyncFolder(Project.Folder)
            || SyncFolders.Containing(Project.Folder, SyncFolders.ForThisMachine()) is not { } sync)
        {
            return;
        }

        _chat.AddNotice(Strings.Format(Strings.NoticeSyncFolder, Project.Name, sync.Service, ProjectFolders.Display(sync.Path)));
        _settings.WarnedSyncFolder(Project.Folder);
    }
}
