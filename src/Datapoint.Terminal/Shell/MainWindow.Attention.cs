using System.ComponentModel;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell.Attention;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    // When the agent starts waiting for the user, the title says so, and a window that isn't in front also asks the operating
    // system for attention, so a question asked in the background isn't missed.
    private void OnChatPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ChatViewModel.IsWorking), StringComparison.Ordinal))
        {
            NotifyIfFinished();
            return;
        }

        if (!string.Equals(e.PropertyName, nameof(ChatViewModel.NeedsUser), StringComparison.Ordinal))
        {
            return;
        }

        UpdateTitle();
        if (!_chat.NeedsUser || IsActive)
        {
            return;
        }

        WindowAttention.Request(this);
        Notify(_chat.Status == AgentStatus.WaitingForApproval ? Strings.NotifyApproval : Strings.NotifyQuestion);
    }

    private bool _wasWorking;

    private void NotifyIfFinished()
    {
        var finished = _wasWorking && !_chat.IsWorking && !_chat.NeedsUser && _chat.Status == AgentStatus.Idle;
        _wasWorking = _chat.IsWorking;
        if (!finished || IsActive)
        {
            return;
        }

        Notify(Strings.NotifyFinished);
    }

    private void Notify(string message)
    {
        if (!_settings.Notifications)
        {
            return;
        }

        Notifications.DesktopNotifications.Show(Workspace.Name, message);
    }
}
