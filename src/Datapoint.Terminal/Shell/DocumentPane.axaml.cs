using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell.Areas;

namespace Datapoint.Terminal.Shell;

// Product decision 120: one area for each kind of document, with its own header and tabs. The area takes space only while
// something is open in it.
public sealed partial class DocumentPane : UserControl
{
    public DocumentPane()
        : this(AreaKind.Workspace)
    {
    }

    public DocumentPane(AreaKind kind)
    {
        InitializeComponent();
        Kind = kind;
        Title = TitleOf(kind);
        TitleText.Text = Title;
        AutomationProperties.SetName(this, Title);
        AutomationProperties.SetName(Tabs, Title);
        Tabs.ItemsSource = Documents;
        Tabs.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        Documents.CollectionChanged += (_, _) => Summary.Text = MainWindow.OpenCount(Documents.Count);
    }

    public event EventHandler? SelectionChanged;

    public AreaKind Kind { get; }

    public string Title { get; }

    public ObservableCollection<IOpenDocument> Documents { get; } = [];

    public IOpenDocument? Selected => Tabs.SelectedItem as IOpenDocument;

    public Control HeaderControl => Header;

    public ContentControl Page => Host;

    public void Select(IOpenDocument document) => Tabs.SelectedItem = document;

    public static string TitleOf(AreaKind kind) => kind switch
    {
        AreaKind.Hosts => Strings.SidebarHosts,
        AreaKind.Skills => Strings.SidebarSkills,
        AreaKind.ServerFiles => Strings.DocumentsServerFiles,
        _ => Strings.SidebarWorkspaceSection,
    };
}
