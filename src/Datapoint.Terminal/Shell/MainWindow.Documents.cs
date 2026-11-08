using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Hosts;
using Datapoint.Terminal.Agent.Skills;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell.Areas;

namespace Datapoint.Terminal.Shell;

// Product decision 98: Markdown files open in the editor, as tabs (decision 108); other text files open as plain text, and
// anything else in the system's default application. Product decision 120: each kind of document opens in its own area.
public sealed partial class MainWindow
{
    private static readonly string[] s_markdownExtensions = [".md", ".markdown", ".mdown", ".mkd"];

    private readonly Dictionary<AreaKind, DocumentPane> _documentPanes = AreaLayout.DocumentKinds.ToDictionary(kind => kind, kind => new DocumentPane(kind));
    private readonly Dictionary<DocumentViewModel, DocumentTab> _documentTabs = [];
    private NativeMenuItem? _showMarkdownItem;

    // The pane last chosen in, for the menus that act on the document in front when the keyboard is elsewhere.
    private DocumentPane? _activePane;

    private IEnumerable<IOpenDocument> AllDocuments => _documentPanes.Values.SelectMany(pane => pane.Documents);

    private DocumentPane? FocusedPane => _documentPanes.Values.FirstOrDefault(pane => pane.IsKeyboardFocusWithin);

    private DocumentPane? ActivePane => FocusedPane ?? (_activePane is { Documents.Count: > 0 } ? _activePane : null);

    private DocumentTab? ShownDocument => ActivePane?.Selected is DocumentViewModel document ? _documentTabs.GetValueOrDefault(document) : null;

    private void SetUpDocuments()
    {
        foreach (var pane in _documentPanes.Values)
        {
            pane.SelectionChanged += (_, _) => ShowSelectedDocument(pane);
            pane.CollapseRequested += (_, _) => CollapseArea(pane.Kind);
            pane.ExpandRequested = document =>
            {
                ExpandArea(pane.Kind);
                if (document is not null)
                {
                    pane.Select(document);
                }

                FocusShownDocument(pane);
            };
            pane.Documents.CollectionChanged += (_, _) => LayOutPanes();
            pane.GotFocus += (_, _) =>
            {
                _activePane = pane;
                UpdateTitle();
            };
        }

        TerminalArea.GotFocus += (_, _) => UpdateTitle();
        _tabs.CollectionChanged += (_, _) => RefreshDocumentHosts();
        _tabs.CollectionChanged += (_, _) => ShowTerminalCount();
        ShowTerminalCount();
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
        if (AllDocuments.OfType<DocumentViewModel>().FirstOrDefault(document => string.Equals(document.Path, path, StringComparison.Ordinal)) is { } open)
        {
            ShowDocument(open);
            return;
        }

        var isMarkdown = s_markdownExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        var file = MarkdownFile.Open(path, out var problem, splitsHeader: IsSkillFile(path));
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
        document.Skill = SkillHeaderFor(document);
        _documentTabs[document] = new DocumentTab(document);
        ShowDocument(document);
    }

    // A host's page, a skill, a file of the workspace's own, or a file on a server.
    private DocumentPane PaneFor(IOpenDocument document) => _documentPanes[document switch
    {
        ServerFileViewModel => AreaKind.ServerFiles,
        DocumentViewModel { Host: not null } => AreaKind.Hosts,
        DocumentViewModel skill when IsSkillFile(skill.Path) => AreaKind.Skills,
        _ => AreaKind.Workspace,
    }];

    private DocumentPane? PaneHolding(IOpenDocument document) => _documentPanes.Values.FirstOrDefault(pane => pane.Documents.Contains(document));

    // In its pane, added if it isn't there yet, and in front, with the pane expanded: the user asked to see it.
    private void ShowDocument(IOpenDocument document)
    {
        var pane = PaneHolding(document) ?? PaneFor(document);
        ExpandArea(pane.Kind);
        if (!pane.Documents.Contains(document))
        {
            pane.Documents.Add(document);
        }

        _activePane = pane;
        pane.Select(document);
    }

    private void ShowSelectedDocument(DocumentPane pane)
    {
        pane.Page.Content = ViewOf(pane.Selected);
        if (pane.Selected is null)
        {
            return;
        }

        _activePane = pane;
        _showMarkdownItem?.IsEnabled = ShownDocument is { Document.CanShowMarkdown: true };
        _showMarkdownItem?.IsChecked = ShownDocument?.Document.IsSourceMode == true;
        UpdateTitle();
        Dispatcher.UIThread.Post(() => FocusShownDocument(pane), DispatcherPriority.Background);
    }

    private Control? ViewOf(IOpenDocument? document) => document switch
    {
        DocumentViewModel open when _documentTabs.TryGetValue(open, out var tab) => tab.View,
        ServerFileViewModel file when _hostFiles.TryGetValue(file, out var open) => open.View,
        _ => null,
    };

    private void CloseDocument(DocumentViewModel document)
    {
        if (!_documentTabs.TryGetValue(document, out var tab))
        {
            return;
        }

        if (!tab.SaveNow())
        {
            // Unsaved changes stay open until the user settles the conflict or the problem.
            ShowDocument(document);
            return;
        }

        _documentTabs.Remove(document);
        tab.Dispose();
        RemoveFromStrip(document);
    }

    // The tab beside a closed one comes to the front; when it was the last, the pane goes and the keyboard returns to the terminal.
    private void RemoveFromStrip(IOpenDocument document)
    {
        if (PaneHolding(document) is not { } pane)
        {
            return;
        }

        var index = pane.Documents.IndexOf(document);
        var wasShown = ReferenceEquals(pane.Selected, document);
        pane.Documents.Remove(document);
        if (!wasShown)
        {
            return;
        }

        if (pane.Documents.Count > 0)
        {
            pane.Select(pane.Documents[Math.Min(index, pane.Documents.Count - 1)]);
            return;
        }

        SelectedTab?.View.Focus();
    }

    // Cmd+W closes the tab in the pane that has the keyboard: the plan's, a document's, or the terminal's.
    private void CloseShownTab()
    {
        switch (PlanArea.IsKeyboardFocusWithin ? _planReview : FocusedPane?.Selected)
        {
            case DocumentViewModel document:
                CloseDocument(document);
                break;
            case ServerFileViewModel file:
                CloseHostFile(file);
                break;
            case Datapoint.Terminal.Chat.PlanReviewViewModel plan:
                ClosePlan(plan);
                break;
            default:
                CloseSelectedTab();
                break;
        }
    }

    // The workspace's files are named for what they are, without extensions (product decision 100).
    private string? TitleFor(string path)
    {
        if (string.Equals(path, InstructionsPath, StringComparison.Ordinal))
        {
            return Strings.SidebarInstructions;
        }

        if (IsSkillFile(path))
        {
            return Datapoint.Terminal.Sidebar.SkillListing.NameOf(path);
        }

        var docs = CreateHostDocs();
        var connections = Connections.Count > 0 ? Connections : Workspace.Settings.Connections;
        return connections.FirstOrDefault(connection => string.Equals(docs.FileFor(connection.Name), path, StringComparison.Ordinal))?.Name;
    }

    private bool IsSkillFile(string path) =>
        string.Equals(Path.GetFileName(path), SkillDocument.FileName, StringComparison.Ordinal)
        && string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(path)), _sidebar.SkillsFolder, StringComparison.Ordinal);

    // Product decision 103: a skill's page, with where it runs and how it rolls out as a form above its steps.
    private SkillHeaderViewModel? SkillHeaderFor(DocumentViewModel document)
    {
        if (!IsSkillFile(document.Path))
        {
            return null;
        }

        var skill = SkillDocument.Read(document.Header + document.Text, document.Path);
        var location = "skills/" + Path.GetFileName(Path.GetDirectoryName(document.Path));
        return new SkillHeaderViewModel(skill, location, !Workspace.IsReadOnly, () => Connections.Count > 0 ? Connections : Workspace.Settings.Connections)
        {
            Saving = (labels, rollout, description) =>
            {
                // A SKILL.md written by hand without a header gets one, with the name its folder has.
                var current = document.Header.Length > 0 ? document.Header : SkillDocument.Create(Path.GetFileName(Path.GetDirectoryName(document.Path))!, description, [], rollout, string.Empty);
                return document.SaveHeader(SkillDocument.WithSettings(current, labels, rollout, description)) == SaveResult.Saved;
            },
            RunRequested = hosts => DraftInChat(hosts.Count == 0
                ? Strings.Format(Strings.SkillRunDraft, skill.Title)
                : Strings.Format(Strings.SkillRunDraftHosts, skill.Title, string.Join(", ", hosts))),
        };
    }

    private HostHeaderViewModel? HostHeaderFor(string path)
    {
        var docs = CreateHostDocs();
        var connections = Connections.Count > 0 ? Connections : Workspace.Settings.Connections;
        if (connections.FirstOrDefault(connection => string.Equals(docs.FileFor(connection.Name), path, StringComparison.Ordinal)) is not { } connection)
        {
            return null;
        }

        var header = new HostHeaderViewModel(connection.Name, connection.Environment, AddressOf(connection), () => _ = OpenConnectionAsync(connection))
        {
            OpenText = OpenTextFor(connection.Name),
            Labels = connection.Labels ?? [],
        };
        header.SetLog(HostLog.Read(docs.LogFor(connection.Name)), Environment.UserName);
        return header;
    }

    // After the agent logs a change, or the window comes back to the front: a teammate's entry may have arrived.
    private void RefreshHostLogs()
    {
        var docs = CreateHostDocs();
        foreach (var host in AllDocuments.OfType<DocumentViewModel>().Select(document => document.Host).OfType<HostHeaderViewModel>())
        {
            host.SetLog(HostLog.Read(docs.LogFor(host.Name)), Environment.UserName);
        }
    }

    private void RefreshDocumentHosts()
    {
        foreach (var host in AllDocuments.OfType<DocumentViewModel>().Select(document => document.Host).OfType<HostHeaderViewModel>())
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
