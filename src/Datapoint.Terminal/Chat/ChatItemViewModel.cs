using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Datapoint.Terminal.Chat;

public sealed partial class ChatItemViewModel(ChatItemKind kind, string text) : ObservableObject
{
    public ChatItemKind Kind { get; } = kind;

    // For a command, the terminal it ran in, so a mark in that terminal can lead back here.
    public string? Terminal { get; init; }

    // Briefly set when the user follows a terminal's mark to this step.
    [ObservableProperty]
    public partial bool IsHighlighted { get; set; }

    public bool IsUser => Kind == ChatItemKind.User;

    public bool IsAgent => Kind == ChatItemKind.Agent;

    public bool IsCommand => Kind == ChatItemKind.Command;

    public bool IsNotice => Kind == ChatItemKind.Notice;

    public bool IsPlan => Kind == ChatItemKind.Plan;

    // For an approved plan, its steps, ticked off as the agent carries them out.
    public IReadOnlyList<PlanStepViewModel> Steps { get; init; } = [];

    [ObservableProperty]
    public partial string Text { get; set; } = text;

    [ObservableProperty]
    public partial string Detail { get; set; } = string.Empty;

    // An optional button under a notice, e.g. to show what changed.
    public string? ActionLabel { get; init; }

    public ICommand? ActionCommand { get; init; }

    public bool HasAction => ActionCommand is not null;
}
