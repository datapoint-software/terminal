using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;

namespace Datapoint.Terminal.Chat;

public sealed partial class PlanReviewView : UserControl
{
    private const int MaxQuote = 300;

    // Narrower than this, the comments go below the plan rather than beside it, so the plan keeps a readable width.
    private const double CommentsBesideWidth = 680;

    // A text block loses its selection when it loses the keyboard, as it does when a step's Comment button is clicked, so the
    // selection is kept from the last time the user selected something in the plan.
    private (string Text, int? Step)? _lastSelection;

    public PlanReviewView()
    {
        InitializeComponent();
        var comment = new RelayCommand(CommentOnSelection, () => Review is { IsWaiting: true } && Selection() is not null);
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.M, KeyModifiers.Control | KeyModifiers.Alt), Command = comment });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.M, KeyModifiers.Meta | KeyModifiers.Alt), Command = comment });
        PlanBody.AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        PlanBody.AddHandler(KeyUpEvent, (_, _) => _lastSelection = Selection(), RoutingStrategies.Tunnel, handledEventsToo: true);
        PlanScroll.ScrollChanged += (_, _) => CommentSelectionButton.IsVisible = false;
        SizeChanged += (_, e) => PlaceComments(e.NewSize.Width >= CommentsBesideWidth);
    }

    private void PlaceComments(bool beside)
    {
        DockPanel.SetDock(CommentsPane, beside ? Dock.Right : Dock.Bottom);
        CommentsPane.Width = beside ? 260 : double.NaN;
        CommentsPane.MaxHeight = beside ? double.PositiveInfinity : 220;
        CommentsPane.BorderThickness = beside ? new Thickness(1, 0, 0, 0) : new Thickness(0, 1, 0, 0);
    }

    private PlanReviewViewModel? Review => DataContext as PlanReviewViewModel;

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if ((e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        var at = e.GetPosition(SelectionLayer);
        Dispatcher.UIThread.Post(() => ShowCommentButton(at));
    }

    // Just above where the selection ended, kept inside the page.
    private void ShowCommentButton(Point at)
    {
        _lastSelection = Selection();
        if (Review is not { IsWaiting: true } || _lastSelection is null)
        {
            CommentSelectionButton.IsVisible = false;
            return;
        }

        CommentSelectionButton.IsVisible = true;
        Canvas.SetLeft(CommentSelectionButton, Math.Clamp(at.X - 36, 8, Math.Max(8, SelectionLayer.Bounds.Width - 90)));
        Canvas.SetTop(CommentSelectionButton, Math.Clamp(at.Y - 40, 0, Math.Max(0, SelectionLayer.Bounds.Height - 40)));
    }

    // The selected text, and the step it's in when it's all in one.
    private (string Text, int? Step)? Selection()
    {
        var blocks = PlanBody.GetVisualDescendants().OfType<SelectableTextBlock>().Where(block => !string.IsNullOrWhiteSpace(block.SelectedText)).ToList();
        if (blocks.Count == 0)
        {
            return null;
        }

        var steps = blocks.Select(block => (block.DataContext as PlanStepViewModel)?.Number).Distinct().ToList();
        var text = string.Join(' ', blocks.Select(block => block.SelectedText.Trim()));
        return (text.Length > MaxQuote ? text[..MaxQuote] + "…" : text, steps.Count == 1 ? steps[0] : null);
    }

    private void OnCommentOnSelection(object? sender, RoutedEventArgs e) => CommentOnSelection();

    private void CommentOnSelection()
    {
        if (Review is not { IsWaiting: true } review || (Selection() ?? _lastSelection) is not { } selection)
        {
            return;
        }

        Comment(review, selection.Text, selection.Step);
    }

    private void OnCommentOnStep(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PlanStepViewModel step } || Review is not { } review)
        {
            return;
        }

        var quote = _lastSelection is { } selection && selection.Step == step.Number ? selection.Text : null;
        Comment(review, quote, step.Number);
    }

    private void Comment(PlanReviewViewModel review, string? quote, int? step)
    {
        var comment = review.AddComment(quote, step);
        _lastSelection = null;
        CommentSelectionButton.IsVisible = false;
        foreach (var block in PlanBody.GetVisualDescendants().OfType<SelectableTextBlock>())
        {
            block.ClearSelection();
        }

        Dispatcher.UIThread.Post(() => FocusComment(comment), DispatcherPriority.Background);
    }

    private void FocusComment(PlanCommentViewModel comment)
    {
        if (CommentList.ContainerFromItem(comment)?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is not { } box)
        {
            return;
        }

        box.BringIntoView();
        box.Focus();
    }
}
