using System.ComponentModel;
using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Shell.Attention;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    // When the agent starts waiting for the user, the title says so, and a window that isn't in front also asks the operating
    // system for attention, so a question asked in the background isn't missed.
    private void OnChatPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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
    }
}
