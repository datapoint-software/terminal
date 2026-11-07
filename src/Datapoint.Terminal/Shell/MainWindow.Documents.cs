using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 98: Markdown files open in the editor, as tabs after the terminals; other text files open as plain text, and
// anything else in the system's default application.
public sealed partial class MainWindow
{
    private static readonly string[] s_markdownExtensions = [".md", ".markdown", ".mdown", ".mkd"];

    private readonly ObservableCollection<IOpenDocument> _documents = [];
    private readonly Dictionary<DocumentViewModel, DocumentTab> _documentTabs = [];
    private NativeMenuItem? _showMarkdownItem;

    private DocumentTab? ShownDocument => DocumentTabs.SelectedItem is DocumentViewModel document ? _documentTabs.GetValueOrDefault(document) : null;

    private void SetUpDocuments()
    {
        DocumentTabs.ItemsSource = _documents;
        DocumentTabs.SelectionChanged += (_, _) => ShowSelectedDocument();
        Tabs.AddHandler(PointerReleasedEvent, (_, _) => HideDocument(), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        _tabs.CollectionChanged += (_, _) => RefreshDocumentHosts();
        Closing += (_, _) =>
        {
            foreach (var tab in _documentTabs.Values)
            {
                tab.SaveNow();
            }
        };
        Closed += (_, _) =>
        {
            foreach (var tab in _documentTabs.Values)
            {
                tab.Dispose();
            }
        };
    }

    private void OpenWorkspaceFile(string path)
    {
        if (_documents.OfType<DocumentViewModel>().FirstOrDefault(document => string.Equals(document.Path, path, StringComparison.Ordinal)) is { } open)
        {
            DocumentTabs.SelectedItem = open;
            return;
        }

        var isMarkdown = s_markdownExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        var file = MarkdownFile.Open(path, out var problem);
        if (file is null)
        {
            if (isMarkdown)
            {
                _chat.AddNotice(Strings.Format(Strings.DocumentCantOpen, Path.GetFileName(path), problem));
                return;
            }

            // Not text the editor can show: the system's own application for it.
            _ = Launcher.LaunchFileInfoAsync(new FileInfo(path));
            return;
        }

        var document = new DocumentViewModel(file, isPlainText: !isMarkdown)
        {
            CloseRequested = CloseDocument,
            Host = HostHeaderFor(path),
            Title = TitleFor(path),
        };
        _documentTabs[document] = new DocumentTab(document);
        _documents.Add(document);
        DocumentTabs.SelectedItem = document;
    }

    private void ShowSelectedDocument()
    {
        var shown = ShownDocument;
        var hostFile = ShownHostFile;
        DocumentHost.Content = (Control?)shown?.View ?? hostFile;
        DocumentHost.IsVisible = DocumentHost.Content is not null;
        Tabs.Classes.Set("behind", DocumentHost.Content is not null);
        if (hostFile is not null)
        {
            Dispatcher.UIThread.Post(hostFile.FocusText, DispatcherPriority.Background);
        }

        _showMarkdownItem?.IsEnabled = shown is { Document.CanShowMarkdown: true };
        _showMarkdownItem?.IsChecked = shown?.Document.IsSourceMode == true;
        if (shown is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(shown.View.FocusText, DispatcherPriority.Background);
    }

    // The terminals come back to the front: the document stays open in its tab.
    private void HideDocument()
    {
        if (DocumentTabs.SelectedItem is null)
        {
            return;
        }

        ShownDocument?.SaveNow();
        DocumentTabs.SelectedItem = null;
    }

    private void CloseDocument(DocumentViewModel document)
    {
        if (!_documentTabs.TryGetValue(document, out var tab))
        {
            return;
        }

        if (!tab.SaveNow())
        {
            // Unsaved changes stay open until the user settles the conflict or the problem.
            DocumentTabs.SelectedItem = document;
            return;
        }

        _documentTabs.Remove(document);
        tab.Dispose();
        RemoveFromStrip(document);
    }

    // The tab beside a closed one comes to the front, or the terminals when it was the last.
    private void RemoveFromStrip(IOpenDocument document)
    {
        var index = _documents.IndexOf(document);
        var wasShown = ReferenceEquals(DocumentTabs.SelectedItem, document);
        _documents.Remove(document);
        if (!wasShown)
        {
            return;
        }

        if (_documents.Count > 0)
        {
            DocumentTabs.SelectedItem = _documents[Math.Min(index, _documents.Count - 1)];
            return;
        }

        SelectedTab?.View.Focus();
    }

    // Cmd+W closes what's in front: the document, if one is shown.
    private void CloseShownTab()
    {
        if (DocumentTabs.SelectedItem is DocumentViewModel document)
        {
            CloseDocument(document);
            return;
        }

        if (DocumentTabs.SelectedItem is ServerFileViewModel file)
        {
            CloseHostFile(file);
            return;
        }

        CloseSelectedTab();
    }

    // The workspace's files are named for what they are, without extensions (product decision 100).
    private string? TitleFor(string path)
    {
        if (string.Equals(path, InstructionsPath, StringComparison.Ordinal))
        {
            return Strings.SidebarInstructions;
        }

        if (string.Equals(Path.GetFileName(path), Datapoint.Terminal.Sidebar.SkillListing.FileName, StringComparison.Ordinal)
            && string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(path)), _sidebar.SkillsFolder, StringComparison.Ordinal))
        {
            return Datapoint.Terminal.Sidebar.SkillListing.NameOf(path);
        }

        var docs = CreateHostDocs();
        var connections = Connections.Count > 0 ? Connections : Workspace.Settings.Connections;
        return connections.FirstOrDefault(connection => string.Equals(docs.FileFor(connection.Name), path, StringComparison.Ordinal))?.Name;
    }

    private HostHeaderViewModel? HostHeaderFor(string path)
    {
        var docs = CreateHostDocs();
        var connections = Connections.Count > 0 ? Connections : Workspace.Settings.Connections;
        if (connections.FirstOrDefault(connection => string.Equals(docs.FileFor(connection.Name), path, StringComparison.Ordinal)) is not { } connection)
        {
            return null;
        }

        return new HostHeaderViewModel(connection.Name, connection.Environment, AddressOf(connection), () =>
        {
            HideDocument();
            _ = OpenConnectionAsync(connection);
        })
        {
            OpenText = OpenTextFor(connection.Name),
        };
    }

    private void RefreshDocumentHosts()
    {
        foreach (var host in _documents.OfType<DocumentViewModel>().Select(document => document.Host).OfType<HostHeaderViewModel>())
        {
            host.OpenText = OpenTextFor(host.Name);
        }
    }

    private string OpenTextFor(string connection)
    {
        var count = _tabs.Count(tab => string.Equals(tab.ConnectionName, connection, StringComparison.OrdinalIgnoreCase));
        return count == 0 ? string.Empty : Strings.Format(Strings.SidebarOpenTerminals, count);
    }

    private static string? AddressOf(WorkspaceConnection connection)
    {
        if (connection.SshHost is { } alias)
        {
            return alias;
        }

        if (connection.Host is not { } host)
        {
            return null;
        }

        var user = connection.User is { } name ? name + "@" : string.Empty;
        var port = connection.Port is { } number ? ":" + number.ToString(CultureInfo.InvariantCulture) : string.Empty;
        return user + host + port;
    }

    private NativeMenu BuildFormatMenu()
    {
        var format = new NativeMenu();
        format.Add(FormatItem(Strings.MenuBold, Key.B, view => view.Format("**")));
        format.Add(FormatItem(Strings.MenuItalic, Key.I, view => view.Format("*")));
        format.Add(FormatItem(Strings.MenuInlineCode, Key.E, view => view.Format("`")));
        format.Add(FormatItem(Strings.MenuLink, Key.K, view => view.FormatLink()));
        return format;
    }

    // Cmd with the letter on macOS, where the menu has the key; elsewhere the editor takes Ctrl with the letter itself, only while
    // it has the keyboard, so the menu just shows it.
    private NativeMenuItem FormatItem(string header, Key key, Func<DocumentView, bool> action)
    {
        var item = new NativeMenuItem(header) { Gesture = new KeyGesture(key, IsMac ? KeyModifiers.Meta : KeyModifiers.Control) };
        item.Click += (_, _) =>
        {
            if (ShownDocument is not { } shown)
            {
                return;
            }

            _ = action(shown.View);
        };
        return item;
    }

    private NativeMenuItem ShowMarkdownMenuItem()
    {
        _showMarkdownItem = Item(Strings.MenuShowMarkdown, Shortcut(Key.M, shift: true), ToggleMarkdownSource);
        _showMarkdownItem.ToggleType = MenuItemToggleType.CheckBox;
        _showMarkdownItem.IsEnabled = false;
        return _showMarkdownItem;
    }

    private void ToggleMarkdownSource()
    {
        if (ShownDocument is not { Document.CanShowMarkdown: true } shown)
        {
            return;
        }

        shown.Document.IsSourceMode = !shown.Document.IsSourceMode;
        _showMarkdownItem?.IsChecked = shown.Document.IsSourceMode;
        Dispatcher.UIThread.Post(shown.View.FocusText, DispatcherPriority.Background);
    }
}
