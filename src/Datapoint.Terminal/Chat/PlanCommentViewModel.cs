using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// A comment on a plan under review: the text the user selected, if any, the step it's on, and what they want changed.
// It's a draft until saved, and sent with the others when the user sends the plan back to be revised.
public sealed partial class PlanCommentViewModel(string? quote, int? step, Action<PlanCommentViewModel> removed) : ObservableObject
{
    private string _saved = string.Empty;

    public string? Quote { get; } = string.IsNullOrWhiteSpace(quote) ? null : quote.Trim();

    public bool HasQuote => Quote is not null;

    public int? Step => step;

    public string WhereText => Step is { } number ? Strings.Format(Strings.PlanCommentStep, number) : Strings.PlanCommentPlan;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    public partial bool IsEditing { get; set; } = true;

    public bool IsShown => !IsEditing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChange), nameof(SentText))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DeleteCommand))]
    public partial bool IsSent { get; set; }

    public bool CanChange => !IsSent;

    public string? SentText => IsSent ? Strings.PlanCommentSent : null;

    // Saved and not sent yet: what Send comments sends.
    public bool IsPending => !IsSent && !IsEditing && _saved.Length > 0;

    public Action? Changed { get; set; }

    // One line of the feedback the agent gets, e.g. On step 2, about "apt-get upgrade": hold back the kernel.
    public string Feedback => (Step is { } number ? $"On step {number}" : "On the plan")
        + (Quote is { } quote ? $", about \"{quote}\"" : string.Empty) + ": " + _saved;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        _saved = Text.Trim();
        IsEditing = false;
        Changed?.Invoke();
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Text);

    // A draft that was never saved goes away; an edit of a saved one goes back to what was saved.
    [RelayCommand]
    private void Cancel()
    {
        if (_saved.Length == 0)
        {
            removed(this);
            return;
        }

        Text = _saved;
        IsEditing = false;
        Changed?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanChange))]
    private void Edit()
    {
        IsEditing = true;
        Changed?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanChange))]
    private void Delete() => removed(this);
}
