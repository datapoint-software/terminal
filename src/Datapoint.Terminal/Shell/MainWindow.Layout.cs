using Avalonia.Controls;
using Avalonia.Input;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell.Areas;

namespace Datapoint.Terminal.Shell;

// Product decisions 104, 109, 119 and 120: the agent, the terminals, each kind of document and the plan sit where the user
// dragged them, the same in every window. A kind of document, and the plan, take space only while one is open. Product decision 115: a shortcut collapses the terminals to a strip
// that names each one and shows what the agent is doing there, and only the user expands them again.
public sealed partial class MainWindow
{
    private AreaNode _layout = AreaLayout.Default();
    private AreaView? _areaView;
    private bool _areTerminalsCollapsed;

    private bool IsShown(AreaKind kind) => kind switch
    {
        AreaKind.Plan => _plans.Count > 0,
        _ when _documentPanes.TryGetValue(kind, out var pane) => pane.Documents.Count > 0,
        _ => true,
    };

    private Control AreaControl(AreaKind kind) => kind switch
    {
        AreaKind.Terminals => TerminalPane,
        AreaKind.Plan => PlanArea,
        _ when _documentPanes.TryGetValue(kind, out var pane) => pane,
        _ => Chat,
    };

    private static string AreaName(AreaKind kind) => kind switch
    {
        AreaKind.Agent => Strings.ChatTitle,
        AreaKind.Terminals => Strings.TabsName,
        AreaKind.Plan => Strings.PlanAreaName,
        _ => DocumentPane.TitleOf(kind),
    };

    // The settings of a version without moving areas say only which side the terminals were on.
    private void SetUpAreas()
    {
        SetUpPlans();
        _layout = AreaLayout.Parse(_settings.AreaLayout) ?? AreaLayout.FromSide(_settings.TerminalSide);
        _areaView = new AreaView(Body, Enum.GetValues<AreaKind>().ToDictionary(kind => kind, AreaControl));
        _areaView.Resized += (_, _) => OnAreaResized();
        Body.SizeChanged += (_, _) => LayOutPanes();
        _settings.AreaLayoutChanged += OnAreaLayoutChanged;
        SetUpAreaDragging();
    }

    private void OnAreaLayoutChanged(object? sender, EventArgs e)
    {
        if (string.Equals(_settings.AreaLayout, AreaLayout.Format(_layout), StringComparison.Ordinal) || AreaLayout.Parse(_settings.AreaLayout) is not { } layout)
        {
            return;
        }

        _layout = layout;
        LayOutPanes();
    }

    // What's shown is a copy brought within the window, so a size saved in a larger window doesn't push an area's edge, and
    // its buttons, out of sight; the saved sizes come back when there's room again. A dragged divider is a choice, so the
    // copy it changed becomes the layout.
    private AreaNode FittedLayout()
    {
        var copy = AreaLayout.Parse(AreaLayout.Format(_layout)) ?? _layout;
        if (Body.Bounds.Width > 0 && Body.Bounds.Height > 0)
        {
            AreaLayout.Fit(copy, Body.Bounds.Width, Body.Bounds.Height, IsShown);
        }

        return copy;
    }

    private void OnAreaResized()
    {
        if (_areaView?.Shown is { } shown)
        {
            _layout = shown;
        }

        SaveLayout();
    }

    private void SaveLayout() => _settings.SetAreaLayout(AreaLayout.Format(_layout));

    // Refused, with a note in the chat, when the window is too small for every area to keep a usable size there.
    private void MoveArea(AreaKind moved, AreaKind? target, AreaEdge edge)
    {
        bool IsShownAfter(AreaKind kind) => kind == moved || IsShown(kind);
        var layout = AreaLayout.Move(_layout, moved, target, edge);
        if (AreaLayout.MinimumLength(layout, isRow: true, IsShownAfter) > Body.Bounds.Width
            || AreaLayout.MinimumLength(layout, isRow: false, IsShownAfter) > Body.Bounds.Height)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeAreaDoesNotFit, AreaName(moved)));
            return;
        }

        UseLayout(layout);
    }

    private void ResetLayout() => UseLayout(AreaLayout.Default());

    private void UseLayout(AreaNode layout)
    {
        AreaLayout.Fit(layout, Body.Bounds.Width, Body.Bounds.Height, IsShown);
        _layout = layout;
        LayOutPanes();
        SaveLayout();
    }

    private void ToggleTerminals()
    {
        if (_areTerminalsCollapsed)
        {
            ShowTerminal(SelectedTab);
            return;
        }

        _areTerminalsCollapsed = true;
        LayOutPanes();
        Chat.FocusInput();
    }

    private void OnCollapseTerminalsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ToggleTerminals();

    private void OnExpandTerminalsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ShowTerminal(SelectedTab);

    private void OnRailTabClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ShowTerminal((sender as Control)?.DataContext as TerminalTab);

    // From the strip, or the chat's card when the agent waits for the user to type in a terminal: the terminals expanded, with
    // that one in front and the keyboard in it.
    private void ShowTerminal(TerminalTab? tab)
    {
        _areTerminalsCollapsed = false;
        LayOutPanes();
        if (tab is not null && _tabs.Contains(tab))
        {
            Tabs.SelectedItem = tab;
        }

        SelectedTab?.View.Focus();
    }

    private void ShowTerminal(string name) => ShowTerminal(_tabs.FirstOrDefault(tab => string.Equals(tab.Name, name, StringComparison.OrdinalIgnoreCase)));

    private void LayOutPanes()
    {
        TerminalArea.IsVisible = !_areTerminalsCollapsed;
        TerminalRail.IsVisible = _areTerminalsCollapsed;
        _areaView?.Show(FittedLayout(), IsShown, _areTerminalsCollapsed ? AreaKind.Terminals : null);
        TerminalRail.Classes.Set("across", _areaView is { IsCollapsedBeside: false });
        UpdateLayoutMenu();
    }

    // The agent's commands lead to their step in the chat; the user's own failed ones offer the agent's help.
    private void OnCommandMarkClicked(TerminalTab tab, Emulator.ShellIntegration.CommandRecord command)
    {
        if (ShowChatStep(tab, command.CommandLine) || command.ExitCode is null or 0)
        {
            return;
        }

        FixCommand(tab, command);
    }

    private bool ShowChatStep(TerminalTab tab, string? commandLine)
    {
        if (commandLine is null || _chat.FindCommand(tab.Name, commandLine) is not { } item)
        {
            return false;
        }

        Chat.Reveal(item);
        return true;
    }

    private NativeMenuItem? _toggleTerminalsItem;

    private NativeMenu BuildViewMenu()
    {
        var view = new NativeMenu();
        _toggleTerminalsItem = Item(Strings.MenuCollapseTerminals, Shortcut(Key.J), ToggleTerminals);
        view.Add(_toggleTerminalsItem);
        view.Add(SidebarMenuItem());
        view.Add(Item(Strings.MenuNextArea, new KeyGesture(Key.F6), () => MoveAreaFocus(1)));
        view.Add(Item(Strings.MenuPreviousArea, new KeyGesture(Key.F6, KeyModifiers.Shift), () => MoveAreaFocus(-1)));
        view.Add(ShowMarkdownMenuItem());
        view.Add(new NativeMenuItemSeparator());
        foreach (var kind in Enum.GetValues<AreaKind>())
        {
            view.Add(MoveAreaMenuItem(kind));
        }

        view.Add(Item(Strings.MenuResetLayout, null, ResetLayout));
        view.Add(new NativeMenuItemSeparator());
        AddAppearanceItems(view);
        return view;
    }

    // The keyboard's way to do what dragging a header does: each place beside the other areas, then the window's edges.
    private NativeMenuItem MoveAreaMenuItem(AreaKind moved)
    {
        var menu = new NativeMenu();
        foreach (var target in Enum.GetValues<AreaKind>().Where(kind => kind != moved))
        {
            var name = AreaName(target);
            menu.Add(Item(Strings.Format(Strings.MenuAreaLeftOf, name), null, () => MoveArea(moved, target, AreaEdge.Left)));
            menu.Add(Item(Strings.Format(Strings.MenuAreaRightOf, name), null, () => MoveArea(moved, target, AreaEdge.Right)));
            menu.Add(Item(Strings.Format(Strings.MenuAreaAbove, name), null, () => MoveArea(moved, target, AreaEdge.Top)));
            menu.Add(Item(Strings.Format(Strings.MenuAreaBelow, name), null, () => MoveArea(moved, target, AreaEdge.Bottom)));
            menu.Add(new NativeMenuItemSeparator());
        }

        menu.Add(Item(Strings.MenuWindowLeft, null, () => MoveArea(moved, null, AreaEdge.Left)));
        menu.Add(Item(Strings.MenuWindowRight, null, () => MoveArea(moved, null, AreaEdge.Right)));
        menu.Add(Item(Strings.MenuWindowTop, null, () => MoveArea(moved, null, AreaEdge.Top)));
        menu.Add(Item(Strings.MenuWindowBottom, null, () => MoveArea(moved, null, AreaEdge.Bottom)));
        return new NativeMenuItem(Strings.Format(Strings.MenuMoveArea, AreaName(moved))) { Menu = menu };
    }

    private void UpdateLayoutMenu() => _toggleTerminalsItem?.Header = _areTerminalsCollapsed ? Strings.MenuExpandTerminals : Strings.MenuCollapseTerminals;

    // F6 moves the keyboard between the window's areas, as in most desktop applications: the chat, the terminals, the
    // documents of each kind and the plan, skipping any that are hidden. Collapsed terminals are reached at the strip's first button.
    private void MoveAreaFocus(int step)
    {
        var areas = new List<(Control Area, Action Focus)> { (Chat, Chat.FocusInput) };
        if (TerminalArea.IsEffectivelyVisible && SelectedTab is { } tab)
        {
            areas.Add((TerminalArea, () => tab.View.Focus()));
        }

        if (TerminalRail.IsEffectivelyVisible)
        {
            areas.Add((TerminalRail, () => ExpandTerminalsButton.Focus()));
        }

        foreach (var pane in _documentPanes.Values.Where(pane => pane.IsEffectivelyVisible))
        {
            areas.Add((pane, () => FocusShownDocument(pane)));
        }

        if (PlanArea.IsEffectivelyVisible)
        {
            areas.Add((PlanArea, () => _planView?.Focus()));
        }

        var current = areas.FindIndex(area => area.Area.IsKeyboardFocusWithin);
        var next = current < 0 ? 0 : (current + step + areas.Count) % areas.Count;
        areas[next].Focus();
    }

    private void FocusShownDocument(DocumentPane pane)
    {
        switch (pane.Selected)
        {
            case DocumentViewModel document when _documentTabs.TryGetValue(document, out var shown):
                shown.View.FocusText();
                break;
            case ServerFileViewModel file when _hostFiles.TryGetValue(file, out var open):
                open.View.FocusText();
                break;
        }
    }
}
