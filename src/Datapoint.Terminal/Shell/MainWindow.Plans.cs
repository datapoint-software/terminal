using Avalonia.Threading;
using Datapoint.Terminal.Chat;

namespace Datapoint.Terminal.Shell;

// Product decision 111: one page in the documents area for the plan under review. Product decision 117: it opens as soon as
// the agent proposes a plan, in front but without taking the keyboard, and follows the agent's revisions, so the user sees
// each new version where they left the last. Asking to show it also puts the keyboard there.
public sealed partial class MainWindow
{
    private PlanReviewViewModel? _planReview;
    private PlanReviewView? _planView;

    private void ShowPlan(ApprovalViewModel plan, bool requested)
    {
        if (_planReview is null)
        {
            _planReview = new PlanReviewViewModel(plan, ClosePlan);
            _planView = new PlanReviewView { DataContext = _planReview };
            _documents.Add(_planReview);
        }
        else if (!ReferenceEquals(_planReview.Plan, plan))
        {
            _planReview.Load(plan);
        }

        DocumentTabs.SelectedItem = _planReview;
        if (!requested)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _planView?.Focus(), DispatcherPriority.Background);
    }

    private void ClosePlan(PlanReviewViewModel review)
    {
        _planReview = null;
        _planView = null;
        RemoveFromStrip(review);
    }
}
