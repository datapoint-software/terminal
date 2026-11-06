using CommunityToolkit.Mvvm.ComponentModel;

namespace Datapoint.Terminal.Chat;

/// <summary>One entry in the chat: a message, a command the agent ran, or a notice from the application.</summary>
public sealed partial class ChatItemViewModel(ChatItemKind kind, string text) : ObservableObject
{
    public ChatItemKind Kind { get; } = kind;

    public bool IsUser => Kind == ChatItemKind.User;

    public bool IsAgent => Kind == ChatItemKind.Agent;

    public bool IsCommand => Kind == ChatItemKind.Command;

    public bool IsNotice => Kind == ChatItemKind.Notice;

    /// <summary>The message, notice, or for a command the command line.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = text;

    /// <summary>For a command: why the agent ran it, and how it ended.</summary>
    [ObservableProperty]
    public partial string Detail { get; set; } = string.Empty;
}
