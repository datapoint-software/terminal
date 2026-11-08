using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// Product decision 111: a plan reviewed as a page in the documents area. The user reads each step, comments on any part of
// it, and either sends the comments back for the agent to revise the plan or approves it. A revision replaces the plan on
// the same page, keeping the comments already sent.
public sealed partial class PlanReviewViewModel : LocalizedObject, IOpenDocument
{
    private readonly Action<PlanReviewViewModel> _close;
    private bool _isSending;

    public PlanReviewViewModel(ApprovalViewModel plan, Action<PlanReviewViewModel> close)
    {
        _close = close;
        Comments.CollectionChanged += (_, _) => Refresh();
        Plan = plan;
        Load(plan);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(ToolTip), nameof(AccessibleName), nameof(CloseName), nameof(RevisionText), nameof(HasRevisionText))]
    public partial ApprovalViewModel Plan { get; private set; }

    public ObservableCollection<PlanCommentViewModel> Comments { get; } = [];

    public bool HasComments => Comments.Count > 0;

    public string Name => Plan.PlanTitle ?? Strings.PlanDocumentName;

    public string ToolTip => Plan.Purpose;

    public string AccessibleName => IsUnsaved ? Strings.Format(Strings.PlanDocumentUnsent, Name) : Strings.Format(Strings.PlanDocumentAccessible, Name);

    public string CloseName => Strings.Format(Strings.DocumentClose, Name);

    // Comments not sent yet show as the tab's dot, like unsaved changes.
    public bool IsUnsaved => PendingCount > 0;

    public int PendingCount => Comments.Count(comment => comment.IsPending);

    private int SentCount => Comments.Count(comment => comment.IsSent);

    public string PendingText => PendingCount switch
    {
        0 when Plan.IsRevision && SentCount > 0 => SentCount == 1 ? Strings.PlanRevisedAfterOne : Strings.Format(Strings.PlanRevisedAfter, SentCount),
        0 => Strings.PlanNoCommentsToSend,
        1 => Strings.PlanOneCommentToSend,
        _ => Strings.Format(Strings.PlanCommentsToSend, PendingCount),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWaiting), nameof(StatusText), nameof(IsApproved))]
    [NotifyCanExecuteChangedFor(nameof(SendCommentsCommand), nameof(ApproveCommand), nameof(ApproveAnywayCommand), nameof(RejectCommand))]
    public partial PlanReviewState State { get; private set; }

    public bool IsWaiting => State == PlanReviewState.Waiting;

    public bool IsApproved => State == PlanReviewState.Approved;

    public string StatusText => State switch
    {
        PlanReviewState.Approved => Strings.PlanStatusApproved,
        PlanReviewState.SentBack => Strings.PlanStatusSentBack,
        PlanReviewState.Declined => Strings.PlanStatusDeclined,
        PlanReviewState.Withdrawn => Strings.PlanStatusWithdrawn,
        _ => Plan.AddedStep is not null ? Strings.PlanStatusStepAdded : Plan.IsRevision ? Strings.PlanStatusRevised : Strings.PlanStatusWaiting,
    };

    // On a revision, which steps changed, so the user can go straight to them.
    public string? RevisionText => Plan.AddedStep is { } added ? Strings.Format(Strings.PlanStepAddedText, added) : RevisionOf(Plan.PlanSteps, sent: false);

    public bool HasRevisionText => RevisionText is not null;

    // Asked before approving while comments are still unsent, since approving would drop them.
    [ObservableProperty]
    public partial bool IsConfirmingApprove { get; set; }

    // A new plan from the agent: a revision keeps the comments, now sent; any other plan starts a fresh review.
    public void Load(ApprovalViewModel plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsRevision)
        {
            Comments.Clear();
        }

        Plan = plan;
        foreach (var comment in Comments.Where(comment => comment.IsSent))
        {
            comment.Revision = RevisionOf(plan.PlanSteps, sent: true);
        }

        _isSending = false;
        IsConfirmingApprove = false;
        State = PlanReviewState.Waiting;
        OnPropertyChanged(nameof(StatusText));
        Refresh();
        _ = WatchAsync(plan);
    }

    public PlanCommentViewModel AddComment(string? quote, int? step)
    {
        var title = Plan.PlanSteps.FirstOrDefault(item => item.Number == step)?.Title;
        var comment = new PlanCommentViewModel(quote, step, title, remove => Comments.Remove(remove)) { Changed = Refresh };
        Comments.Add(comment);
        return comment;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private void SendComments()
    {
        var pending = Comments.Where(comment => comment.IsPending).ToList();
        Plan.Feedback = Strings.PlanFeedbackIntro + "\n" + string.Join('\n', pending.Select(comment => "- " + comment.Feedback));
        foreach (var comment in pending)
        {
            comment.IsSent = true;
        }

        _isSending = true;
        Plan.DeclineCommand.Execute(null);
        Refresh();
    }

    private bool CanSend() => IsWaiting && PendingCount > 0;

    // Ends the review without a revision: the agent is told not to run or propose the plan again, with any comments the user
    // wrote as their reasons.
    [RelayCommand(CanExecute = nameof(IsWaiting))]
    private void Reject()
    {
        var pending = Comments.Where(comment => comment.IsPending).ToList();
        Plan.Feedback = pending.Count == 0 ? Strings.PlanRejectFeedback
            : Strings.PlanRejectFeedback + "\n" + Strings.PlanRejectComments + "\n" + string.Join('\n', pending.Select(comment => "- " + comment.Feedback));
        foreach (var comment in pending)
        {
            comment.IsSent = true;
        }

        IsConfirmingApprove = false;
        Plan.DeclineCommand.Execute(null);
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(IsWaiting))]
    private void Approve()
    {
        if (PendingCount > 0 || Comments.Any(comment => comment.IsEditing && comment.Text.Trim().Length > 0))
        {
            IsConfirmingApprove = true;
            return;
        }

        Plan.RunCommand.Execute(null);
    }

    [RelayCommand(CanExecute = nameof(IsWaiting))]
    private void ApproveAnyway()
    {
        IsConfirmingApprove = false;
        Plan.RunCommand.Execute(null);
    }

    [RelayCommand]
    private void KeepReviewing() => IsConfirmingApprove = false;

    [RelayCommand]
    private void Close() => _close(this);

    IRelayCommand IOpenDocument.CloseCommand => CloseCommand;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003", Justification = "Completed by the user's decision; nothing waits on a captured context.")]
    // The decision may come from here or from the card in the chat; either way the page says how it ended.
    private async Task WatchAsync(ApprovalViewModel plan)
    {
        PlanReviewState state;
        try
        {
            var decision = await plan.Decision.ConfigureAwait(true);
            state = decision.Approved ? PlanReviewState.Approved : _isSending ? PlanReviewState.SentBack : PlanReviewState.Declined;
        }
        catch (OperationCanceledException)
        {
            state = PlanReviewState.Withdrawn;
        }

        if (!ReferenceEquals(Plan, plan))
        {
            return;
        }

        State = state;
        IsConfirmingApprove = false;
    }

    private void Refresh()
    {
        foreach (var step in Plan.PlanSteps)
        {
            step.CommentCount = Comments.Count(comment => !comment.IsSent && comment.Step == step.Number);
        }

        OnPropertyChanged(nameof(HasComments));
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(PendingText));
        OnPropertyChanged(nameof(IsUnsaved));
        OnPropertyChanged(nameof(AccessibleName));
        SendCommentsCommand.NotifyCanExecuteChanged();
    }

    // Which steps a revision changed, for the page's heading or, after a comment was sent, for its card.
    private static string? RevisionOf(IReadOnlyList<PlanStepViewModel> steps, bool sent)
    {
        var changed = steps.Where(step => step.HasChange).Select(step => step.NumberText).ToList();
        return (changed.Count, sent) switch
        {
            (0, _) => null,
            (1, false) => Strings.Format(Strings.PlanRevisedOne, changed[0]),
            (_, false) => Strings.Format(Strings.PlanRevisedMany, string.Join(", ", changed)),
            (1, true) => Strings.Format(Strings.PlanCommentSentOne, changed[0]),
            _ => Strings.Format(Strings.PlanCommentSentMany, string.Join(", ", changed)),
        };
    }
}
