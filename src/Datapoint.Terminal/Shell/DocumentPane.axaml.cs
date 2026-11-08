using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell.Areas;

namespace Datapoint.Terminal.Shell;

// Product decision 120: one area for each kind of document, with its own header and tabs. The area takes space only while
// something is open in it. Product decision 121: all but the workspace's own files collapse to a strip.
public sealed partial class DocumentPane : UserControl, ILocalizable
{
    public DocumentPane()
        : this(AreaKind.Workspace)
    {
    }

    public DocumentPane(AreaKind kind)
    {
        InitializeComponent();
        Kind = kind;
        Tabs.ItemsSource = Documents;
        RailItems.ItemsSource = Documents;
        CanCollapse = kind != AreaKind.Workspace;
        CollapseButton.IsVisible = CanCollapse;
        ShowNames();
        Localizer.Instance.Register(this);
        CollapseButton.Click += (_, _) => CollapseRequested?.Invoke(this, EventArgs.Empty);
        ExpandButton.Click += (_, _) => ExpandRequested?.Invoke(null);
        Tabs.SelectionChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        Documents.CollectionChanged += (_, _) => Summary.Text = MainWindow.OpenCount(Documents.Count);
    }

    // Product decision 150.
    public void OnLanguageChanged()
    {
        ShowNames();
        Summary.Text = MainWindow.OpenCount(Documents.Count);
    }

    private void ShowNames()
    {
        Title = TitleOf(Kind);
        TitleText.Text = Title;
        AutomationProperties.SetName(this, Title);
        AutomationProperties.SetName(Tabs, Title);
        AutomationProperties.SetName(Rail, Title);
        foreach (var (button, text) in new[] { (CollapseButton, Strings.Format(Strings.CollapseArea, Title)), (ExpandButton, Strings.Format(Strings.ExpandArea, Title)) })
        {
            ToolTip.SetTip(button, text);
            AutomationProperties.SetName(button, text);
        }
    }

    public event EventHandler? SelectionChanged;

    public event EventHandler? CollapseRequested;

    // With the document whose button in the strip was chosen, if one was.
    public Action<IOpenDocument?>? ExpandRequested { get; set; }

    public bool CanCollapse { get; }

    public bool IsCollapsed => Rail.IsVisible;

    public Button ExpandControl => ExpandButton;

    public AreaKind Kind { get; }

    public string Title { get; private set; } = string.Empty;

    public ObservableCollection<IOpenDocument> Documents { get; } = [];

    public IOpenDocument? Selected => Tabs.SelectedItem as IOpenDocument;

    public Control HeaderControl => Header;

    public ContentControl Page => Host;

    public void Select(IOpenDocument document) => Tabs.SelectedItem = document;

    // Across is a strip lying above or below the area it meets, rather than beside it.
    public void SetCollapsed(bool collapsed, bool across)
    {
        Full.IsVisible = !collapsed;
        Rail.IsVisible = collapsed;
        Rail.Classes.Set("across", across);
    }

    private void OnRailTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ExpandRequested?.Invoke((sender as Control)?.DataContext as IOpenDocument);

    public static string TitleOf(AreaKind kind) => kind switch
    {
        AreaKind.Hosts => Strings.SidebarHosts,
        AreaKind.Skills => Strings.SidebarSkills,
        AreaKind.ServerFiles => Strings.DocumentsServerFiles,
        _ => Strings.SidebarWorkspaceSection,
    };
}
