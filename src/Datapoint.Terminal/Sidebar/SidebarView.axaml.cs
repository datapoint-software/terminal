using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Datapoint.Terminal.Sidebar;

// Selecting a row only marks it, as arrow keys do; a click or Enter opens it. The tab in view also selects its host, which
// mustn't open anything.
public sealed partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
        HostList.AddHandler(TappedEvent, OnHostTapped, RoutingStrategies.Bubble);
        HostList.AddHandler(KeyDownEvent, OnHostKeyDown, RoutingStrategies.Bubble);
        FileTree.AddHandler(TappedEvent, OnFileTapped, RoutingStrategies.Bubble);
        FileTree.AddHandler(KeyDownEvent, OnFileKeyDown, RoutingStrategies.Bubble);
    }

    private SidebarViewModel? Model => DataContext as SidebarViewModel;

    private void OnHostTapped(object? sender, TappedEventArgs e)
    {
        if (ItemOf<HostItemViewModel>(e.Source) is not { } host)
        {
            return;
        }

        Model?.ChooseHostCommand.Execute(host);
    }

    private void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Model?.SelectedHost is not { } host)
        {
            return;
        }

        Model.ChooseHostCommand.Execute(host);
        e.Handled = true;
    }

    // The folder's own chevron already opens and closes it.
    private void OnFileTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual visual && visual.FindAncestorOfType<ToggleButton>(includeSelf: true) is not null)
        {
            return;
        }

        if (ItemOf<FileNodeViewModel>(e.Source) is not { } node)
        {
            return;
        }

        Model?.ChooseFileCommand.Execute(node);
    }

    private void OnFileKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Model?.SelectedFile is not { } node)
        {
            return;
        }

        Model.ChooseFileCommand.Execute(node);
        e.Handled = true;
    }

    private static T? ItemOf<T>(object? source)
        where T : class
    {
        for (var element = source as StyledElement; element is not null; element = element.Parent)
        {
            if (element.DataContext is T item)
            {
                return item;
            }
        }

        return null;
    }
}
