using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// One list to click or pick by number; the last row opens a box for an answer in the user's own words, unless the answer
// grants something (web access), which only a choice may do.
public sealed partial class QuestionViewModel : LocalizedObject
{
    private readonly TaskCompletionSource<string?> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public QuestionViewModel(AgentQuestion question, bool allowsOwnWords = true)
    {
        ArgumentNullException.ThrowIfNull(question);
        Text = question.Text;
        Details = question.Details;
        Options = question.Options;
        Rows = [.. question.Options.Select((option, index) => new QuestionRowViewModel(index + 1, option.Label, option.Description, option.IsRecommended)),
            .. question.Options.Count == 0 || !allowsOwnWords ? [] : new[] { new QuestionRowViewModel(question.Options.Count + 1, Strings.QuestionSomethingElse, null, false) { IsOther = true } }];
        IsWriting = Rows.Count == 0;
        KeysText = Strings.Format(Strings.QuestionKeys, Rows.Count);
        var where = question.Terminal is { } terminal ? Strings.Format(Strings.ApprovalInTerminal, terminal) : null;
        where = question.Session is { } session && where is not null ? Strings.Format(Strings.CommandInTerminal, where, session) : where;
        var heading = question.Heading ?? Strings.QuestionHeading;
        HeaderText = where is null ? heading : Strings.Format(Strings.CommandInTerminal, heading, where);
    }

    public string HeaderText { get; }

    public string Text { get; }

    public string? Details { get; }

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    public IReadOnlyList<AgentOption> Options { get; }

    public IReadOnlyList<QuestionRowViewModel> Rows { get; }

    public bool HasOptions => Options.Count > 0;

    public string KeysText { get; }

    // Called on the user interface thread as the user answers (null when they skip), before the agent hears of it.
    public Action<string?>? Answered { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChoosing))]
    public partial bool IsWriting { get; private set; }

    public bool IsChoosing => !IsWriting;

    // An open question has no list to go back to.
    public bool CanGoBack => HasOptions;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial string Draft { get; set; } = string.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003", Justification = "Completed by the user's decision; nothing waits on a captured context.")]
    public Task<string?> Response => _answer.Task;

    public bool IsAnswered => _answer.Task.IsCompleted;

    public void Cancel() => _answer.TrySetCanceled();

    // A number key picks that row, as a click would; false when there's no such row.
    public bool ChooseNumber(int number)
    {
        if (IsWriting || number < 1 || number > Rows.Count)
        {
            return false;
        }

        Choose(Rows[number - 1]);
        return true;
    }

    [RelayCommand]
    private void Choose(QuestionRowViewModel row)
    {
        if (row.IsOther)
        {
            IsWriting = true;
            return;
        }

        Finish(row.Label);
    }

    [RelayCommand]
    private void Back() => IsWriting = !HasOptions;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private void Submit() => Finish(Draft.Trim());

    [RelayCommand]
    private void Skip() => Finish(null);

    private bool CanSubmit() => !string.IsNullOrWhiteSpace(Draft);

    private void Finish(string? answer)
    {
        if (IsAnswered)
        {
            return;
        }

        Answered?.Invoke(answer);
        _answer.TrySetResult(answer);
    }
}
