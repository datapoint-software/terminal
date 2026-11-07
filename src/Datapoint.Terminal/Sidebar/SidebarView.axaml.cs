using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Sidebar;

// Selecting a row only marks it, as arrow keys do; a click or Enter opens it. The tab in view also selects its host, which
// mustn't open anything.
public sealed partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
        HostList.AddHandler(TappedEvent, (_, e) => Open<HostItemViewModel>(e.Source, host => Model?.ChooseHostCommand.Execute(host)), RoutingStrategies.Bubble);
        HostList.AddHandler(KeyDownEvent, (_, e) => OnEnter(e, Model?.SelectedHost, host => Model?.ChooseHostCommand.Execute(host)), RoutingStrategies.Bubble);
        SkillList.AddHandler(TappedEvent, (_, e) => Open<SkillItemViewModel>(e.Source, skill => Model?.ChooseSkillCommand.Execute(skill)), RoutingStrategies.Bubble);
        SkillList.AddHandler(KeyDownEvent, (_, e) => OnEnter(e, Model?.SelectedSkill, skill => Model?.ChooseSkillCommand.Execute(skill)), RoutingStrategies.Bubble);
        SwitcherButton.Click += (_, _) => ShowSwitcher();
    }

    private SidebarViewModel? Model => DataContext as SidebarViewModel;

    // Built when it opens, so it lists the workspaces as they are then.
    public MenuFlyout? BuildSwitcherMenu()
    {
        if (Model is not { } model)
        {
            return null;
        }

        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        foreach (var workspace in model.ListWorkspaces?.Invoke() ?? [])
        {
            var item = new MenuItem { Header = WorkspaceRow(workspace), Icon = workspace.IsCurrent ? CheckMark() : null };
            AutomationProperties.SetName(item, $"{workspace.Name}, {workspace.Detail}");
            item.Click += (_, _) => model.WorkspaceChosen?.Invoke(workspace);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Strings.SidebarNewWorkspace, model.NewWorkspaceRequested));
        menu.Items.Add(Item(Strings.SidebarOpenWorkspace, model.OpenWorkspaceRequested));
        menu.Items.Add(new Separator());
        var reveal = OperatingSystem.IsMacOS() ? Strings.SidebarRevealMac : OperatingSystem.IsWindows() ? Strings.SidebarRevealWindows : Strings.SidebarReveal;
        menu.Items.Add(Item(Strings.Format(reveal, model.WorkspaceName), model.RevealRequested));
        return menu;
    }

    private void ShowSwitcher() => BuildSwitcherMenu()?.ShowAt(SwitcherButton);

    private static MenuItem Item(string header, Action? action)
    {
        var item = new MenuItem { Header = header, IsEnabled = action is not null };
        item.Click += (_, _) => action?.Invoke();
        return item;
    }

    private static Grid WorkspaceRow(WorkspaceSwitchItem workspace)
    {
        var badge = new Components.Avatar { Text = workspace.Initials };
        var detail = new TextBlock { Text = workspace.Detail, TextWrapping = TextWrapping.NoWrap, Classes = { "secondary" } };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, [Grid.ColumnProperty] = 1 };
        text.Children.Add(new TextBlock { Text = workspace.Name, Classes = { "title" } });
        text.Children.Add(detail);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, MinWidth = 220, Children = { badge, text } };
    }

    private static Avalonia.Controls.Shapes.Path CheckMark()
    {
        var mark = new Avalonia.Controls.Shapes.Path { Width = 16, Height = 16, StrokeThickness = 1.5, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, Data = Geometry.Parse("M3.5,8.5 L6.5,11.5 L12.5,4.5") };
        mark.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty, mark.GetResourceObservable("AccentBrush"));
        return mark;
    }

    private static void Open<T>(object? source, Action<T> open)
        where T : class
    {
        for (var element = source as StyledElement; element is not null; element = element.Parent)
        {
            if (element.DataContext is T item)
            {
                open(item);
                return;
            }
        }
    }

    private static void OnEnter<T>(KeyEventArgs e, T? selected, Action<T> open)
        where T : class
    {
        if (e.Key != Key.Enter || selected is null)
        {
            return;
        }

        open(selected);
        e.Handled = true;
    }
}
