using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell.Areas;

namespace Datapoint.Terminal.Shell;

// Product decision 111: one page for the plan under review. Product decision 117: it opens as soon as the agent proposes a
// plan, without taking the keyboard, and follows the agent's revisions, so the user sees each new version where they left
// the last. Asking to show it also puts the keyboard there. Product decision 119: the page has an area of its own, which
// takes space only while a plan is open and can be moved like any other. Its header names it and closes it; there's no tab,
// as there's only ever one plan. Product decision 121: it collapses to a strip, and stays collapsed when the agent proposes a
// new version, until the user asks to see it.
public sealed partial class MainWindow
{
    private readonly ObservableCollection<IOpenDocument> _plans = [];
    private PlanReviewViewModel? _planReview;
    private PlanReviewView? _planView;

    private void SetUpPlans()
    {
        _plans.CollectionChanged += (_, _) => LayOutPanes();
        PlanArea.GotFocus += (_, _) => UpdateTitle();
        SetUpPlanButtons();
    }

    private void ShowPlan(ApprovalViewModel plan, bool requested)
    {
        if (_planReview is null)
        {
            _planReview = new PlanReviewViewModel(plan, ClosePlan);
            _planReview.PropertyChanged += (_, _) => ShowPlanSummary();
            _planView = new PlanReviewView { DataContext = _planReview };
            PlanHost.Content = _planView;
            _plans.Add(_planReview);
        }
        else if (!ReferenceEquals(_planReview.Plan, plan))
        {
            _planReview.Load(plan);
        }

        ShowPlanSummary();
        if (!requested)
        {
            return;
        }

        ExpandArea(AreaKind.Plan);
        Dispatcher.UIThread.Post(() => _planView?.Focus(), DispatcherPriority.Background);
    }

    // The page itself has the plan's title; the header says where its review stands.
    private void ShowPlanSummary()
    {
        PlanSummary.Text = _planReview?.StatusText ?? string.Empty;
        var close = _planReview?.CloseName ?? string.Empty;
        ToolTip.SetTip(ClosePlanButton, close);
        Avalonia.Automation.AutomationProperties.SetName(ClosePlanButton, close);
        PlanRailWaiting.IsVisible = _planReview is { IsWaiting: true };
        ToolTip.SetTip(PlanRailTab, PlanSummary.Text);
        Avalonia.Automation.AutomationProperties.SetName(PlanRailTab, Strings.PlanAreaName + ", " + PlanSummary.Text);
    }

    private void SetUpPlanButtons()
    {
        foreach (var (button, text) in new[] { ((Control)CollapsePlanButton, Strings.Format(Strings.CollapseArea, Strings.PlanAreaName)), (ExpandPlanButton, Strings.Format(Strings.ExpandArea, Strings.PlanAreaName)) })
        {
            ToolTip.SetTip(button, text);
            Avalonia.Automation.AutomationProperties.SetName(button, text);
        }
    }

    private void OnCollapsePlanClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CollapseArea(AreaKind.Plan);

    private void OnExpandPlanClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ExpandArea(AreaKind.Plan);
        _planView?.Focus();
    }

    private void OnClosePlanClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_planReview is not { } review)
        {
            return;
        }

        ClosePlan(review);
    }

    // The keyboard goes back to the chat, where the conversation about the plan goes on.
    private void ClosePlan(PlanReviewViewModel review)
    {
        var hadKeyboard = PlanArea.IsKeyboardFocusWithin;
        _planReview = null;
        _planView = null;
        PlanHost.Content = null;
        _plans.Remove(review);
        if (!hadKeyboard)
        {
            return;
        }

        Chat.FocusInput();
    }
}
