namespace Datapoint.Terminal.Sidebar;

public sealed record ProjectEntry(string Name, string Path, bool IsFolder)
{
    // Dotfiles are shown quieter: they're usually settings, not the project's content.
    public bool IsQuiet => Name.StartsWith('.');
}
