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

    public bool IsAsked => Kind == ChatItemKind.Asked;

    public bool IsWeb => Kind == ChatItemKind.Web;

    // Something the user should know about a web item, such as text in it aimed at the model.
    public string? Warning { get; init; }

    public bool HasWarning => Warning is not null;

    // For an approved plan, its progress, shown here once it's no longer pinned above the conversation.
    public PlanProgressViewModel? Plan { get; init; }

    public RolloutProgressViewModel? Rollout { get; init; }

    [ObservableProperty]
    public partial string Text { get; set; } = text;

    [ObservableProperty]
    public partial string Detail { get; set; } = string.Empty;

    // An optional button under a notice, e.g. to show what changed.
    public string? ActionLabel { get; init; }

    public ICommand? ActionCommand { get; init; }

    public bool HasAction => ActionCommand is not null;
}
