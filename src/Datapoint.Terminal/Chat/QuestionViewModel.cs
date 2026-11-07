using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;

namespace Datapoint.Terminal.Chat;

public sealed partial class QuestionViewModel : ObservableObject
{
    private readonly TaskCompletionSource<string?> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public QuestionViewModel(AgentQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        Text = question.Text;
        Details = question.Details;
        Options = question.Options;
    }

    public string Text { get; }

    public string? Details { get; }

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    public IReadOnlyList<AgentOption> Options { get; }

    public bool HasOptions => Options.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial string Draft { get; set; } = string.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003", Justification = "Completed by the user's decision; nothing waits on a captured context.")]
    public Task<string?> Response => _answer.Task;

    public void Cancel() => _answer.TrySetCanceled();

    [RelayCommand]
    private void Choose(AgentOption option) => _answer.TrySetResult(option.Label);

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private void Submit() => _answer.TrySetResult(Draft.Trim());

    [RelayCommand]
    private void Skip() => _answer.TrySetResult(null);

    private bool CanSubmit() => !string.IsNullOrWhiteSpace(Draft);
}
