using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Datapoint.Terminal.Sidebar;

// A file or folder in the sidebar's tree. A folder lists what's in it only once it's opened, so a large workspace costs nothing
// until the user looks; until then it holds one placeholder, so the tree still shows that it can be opened.
public sealed partial class FileNodeViewModel : ObservableObject
{
    private readonly string _workspaceFolder;
    private bool _isLoaded;

    public FileNodeViewModel(WorkspaceEntry entry, string workspaceFolder, string hostsFolder)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _workspaceFolder = workspaceFolder;
        HostsFolder = hostsFolder;
        Name = entry.Name;
        Path = entry.Path;
        IsFolder = entry.IsFolder;
        IsQuiet = entry.IsQuiet;
        IsHostDocument = !IsFolder
            && string.Equals(System.IO.Path.GetExtension(Name), ".md", StringComparison.OrdinalIgnoreCase)
            && string.Equals(System.IO.Path.GetDirectoryName(Path), hostsFolder, StringComparison.Ordinal);
        if (!IsFolder)
        {
            return;
        }

        Children.Add(Placeholder);
    }

    private FileNodeViewModel()
    {
        _workspaceFolder = string.Empty;
        HostsFolder = string.Empty;
        Name = string.Empty;
        Path = string.Empty;
    }

    private static FileNodeViewModel Placeholder { get; } = new();

    public string Name { get; }

    public string Path { get; }

    public string HostsFolder { get; }

    public bool IsFolder { get; }

    public bool IsQuiet { get; }

    // A file in hosts/: a host's documentation.
    public bool IsHostDocument { get; }

    // Writing (Markdown or plain text) shows as a page with lines; anything else as a plain page.
    public bool IsDocument => !IsFolder && !IsHostDocument && IsWriting(Name);

    public bool IsOtherFile => !IsFolder && !IsHostDocument && !IsWriting(Name);

    public bool IsMarkdown => !IsFolder && System.IO.Path.GetExtension(Name).ToUpperInvariant() is ".MD" or ".MARKDOWN";

    private static bool IsWriting(string name) => System.IO.Path.GetExtension(name).ToUpperInvariant() is ".MD" or ".MARKDOWN" or ".TXT";

    public ObservableCollection<FileNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value || _isLoaded)
        {
            return;
        }

        Reload();
    }

    // Lists the folder again, keeping the nodes still there, so what the user opened stays open.
    public void Reload()
    {
        if (!IsFolder)
        {
            return;
        }

        _isLoaded = true;
        Merge(Children, WorkspaceListing.List(Path, _workspaceFolder), _workspaceFolder, HostsFolder);
    }

    // Lists again only what's been opened: a closed folder will list itself when it's opened.
    public void Refresh()
    {
        if (!_isLoaded)
        {
            return;
        }

        Reload();
        foreach (var child in Children)
        {
            child.Refresh();
        }
    }

    internal static void Merge(ObservableCollection<FileNodeViewModel> nodes, IReadOnlyList<WorkspaceEntry> entries, string workspaceFolder, string hostsFolder)
    {
        var existing = nodes.Where(node => !ReferenceEquals(node, Placeholder)).ToDictionary(node => (node.Path, node.IsFolder));
        var merged = entries.Select(entry => existing.TryGetValue((entry.Path, entry.IsFolder), out var node) ? node : new FileNodeViewModel(entry, workspaceFolder, hostsFolder)).ToList();
        for (var index = nodes.Count - 1; index >= 0; index--)
        {
            if (!merged.Contains(nodes[index]))
            {
                nodes.RemoveAt(index);
            }
        }

        for (var index = 0; index < merged.Count; index++)
        {
            var at = nodes.IndexOf(merged[index]);
            if (at == index)
            {
                continue;
            }

            if (at >= 0)
            {
                nodes.Move(at, index);
                continue;
            }

            nodes.Insert(index, merged[index]);
        }
    }
}
