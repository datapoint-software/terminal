using System.Collections.ObjectModel;
using Avalonia.Threading;
using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 111: one page for the plan under review. Product decision 117: it opens as soon as the agent proposes a
// plan, without taking the keyboard, and follows the agent's revisions, so the user sees each new version where they left
// the last. Asking to show it also puts the keyboard there. Product decision 119: the page has an area of its own, which
// takes space only while a plan is open and can be moved like any other.
public sealed partial class MainWindow
{
    private readonly ObservableCollection<IOpenDocument> _plans = [];
    private PlanReviewViewModel? _planReview;
    private PlanReviewView? _planView;

    private void SetUpPlans()
    {
        PlanTabs.ItemsSource = _plans;
        _plans.CollectionChanged += (_, _) => LayOutPanes();
        PlanArea.GotFocus += (_, _) => UpdateTitle();
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

        PlanTabs.SelectedItem = _planReview;
        ShowPlanSummary();
        if (!requested)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _planView?.Focus(), DispatcherPriority.Background);
    }

    private void ShowPlanSummary() => PlanSummary.Text = _planReview?.StatusText ?? string.Empty;

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
