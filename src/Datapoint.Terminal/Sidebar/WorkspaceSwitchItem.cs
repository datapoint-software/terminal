namespace Datapoint.Terminal.Sidebar;

// One workspace in the switcher's menu.
public sealed record WorkspaceSwitchItem(string Name, string Detail, string Folder, bool IsCurrent)
{
    public string Initials => WorkspaceLocation.Initials(Name);
}
