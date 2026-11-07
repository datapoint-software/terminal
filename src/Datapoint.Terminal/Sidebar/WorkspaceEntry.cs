namespace Datapoint.Terminal.Sidebar;

public sealed record WorkspaceEntry(string Name, string Path, bool IsFolder)
{
    // Dotfiles are shown quieter: they're usually settings, not the workspace's content.
    public bool IsQuiet => Name.StartsWith('.');
}
