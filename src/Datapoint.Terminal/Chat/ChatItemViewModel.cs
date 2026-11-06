using CommunityToolkit.Mvvm.ComponentModel;

namespace Datapoint.Terminal.Chat;

public sealed partial class ChatItemViewModel(ChatItemKind kind, string text) : ObservableObject
{
    public ChatItemKind Kind { get; } = kind;

    public bool IsUser => Kind == ChatItemKind.User;

    public bool IsAgent => Kind == ChatItemKind.Agent;

    public bool IsCommand => Kind == ChatItemKind.Command;

    public bool IsNotice => Kind == ChatItemKind.Notice;

    [ObservableProperty]
    public partial string Text { get; set; } = text;

    [ObservableProperty]
    public partial string Detail { get; set; } = string.Empty;
}
