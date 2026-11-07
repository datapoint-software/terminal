namespace Datapoint.Terminal.Agent.Workspaces.Ignore;

// Ignore files in precedence order, lowest first. As in Git, once a folder is ignored nothing inside it can be brought back.
public sealed class IgnoreRules(IReadOnlyList<IgnoreFile> files, string root)
{
    public string Root { get; } = WorkspaceFolders.Normalize(root);

    public bool IsIgnored(string path, bool isDirectory)
    {
        if (WorkspaceFolders.RelativeInside(Root, path) is not { } relative)
        {
            return false;
        }

        var current = Root;
        var names = relative.Split('/');
        for (var index = 0; index < names.Length; index++)
        {
            current = Path.Combine(current, names[index]);
            var isLast = index == names.Length - 1;
            if (Decide(current, isLast ? isDirectory : true))
            {
                return true;
            }
        }

        return false;
    }

    // Only files in the path's own folder or above apply to it.
    private bool Decide(string path, bool isDirectory)
    {
        var parent = Path.GetDirectoryName(path)!;
        for (var index = files.Count - 1; index >= 0; index--)
        {
            var file = files[index];
            if (!WorkspaceFolders.IsSameOrInside(parent, file.Folder))
            {
                continue;
            }

            if (file.Decide(path, isDirectory) is { } decision)
            {
                return decision;
            }
        }

        return false;
    }
}
