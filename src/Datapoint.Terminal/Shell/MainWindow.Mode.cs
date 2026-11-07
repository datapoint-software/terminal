using Avalonia.Threading;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 107: one approval mode for the agent, remembered for the workspace, except Ignore permissions, which is
// confirmed every time and never kept.
public sealed partial class MainWindow
{
    private async Task<bool> ConfirmIgnorePermissionsAsync()
    {
        var answer = await ChoiceWindow.AskAsync(
            this,
            Strings.IgnorePermissionsTitle,
            Strings.IgnorePermissionsMessage,
            details: null,
            hasDefault: false,
            Strings.Cancel,
            Strings.IgnorePermissionsConfirm).ConfigureAwait(true);
        return answer == 1;
    }

    private void OnAgentModeChanged(object? sender, EventArgs e)
    {
        var mode = ((Agent.TerminalAgent)sender!).LastingMode;
        Dispatcher.UIThread.Post(() => _settings.SetMode(Workspace.Folder, mode));
    }
}
