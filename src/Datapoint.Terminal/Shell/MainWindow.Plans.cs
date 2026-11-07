using Avalonia.Threading;
using Datapoint.Terminal.Chat;

namespace Datapoint.Terminal.Shell;

// Product decision 111: one page in the documents area for the plan under review. It opens when the user asks to review a
// plan, and once open it follows the agent's revisions, so the user sees each new version where they left the last.
public sealed partial class MainWindow
{
    private PlanReviewViewModel? _planReview;
    private PlanReviewView? _planView;

    private void ShowPlan(ApprovalViewModel plan, bool requested)
    {
        if (_planReview is null)
        {
            if (!requested)
            {
                return;
            }

            _planReview = new PlanReviewViewModel(plan, ClosePlan);
            _planView = new PlanReviewView { DataContext = _planReview };
            _documents.Add(_planReview);
        }
        else if (!ReferenceEquals(_planReview.Plan, plan))
        {
            _planReview.Load(plan);
        }

        if (!requested)
        {
            return;
        }

        DocumentTabs.SelectedItem = _planReview;
        Dispatcher.UIThread.Post(() => _planView?.Focus(), DispatcherPriority.Background);
    }

    private void ClosePlan(PlanReviewViewModel review)
    {
        _planReview = null;
        _planView = null;
        RemoveFromStrip(review);
    }
}
