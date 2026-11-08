using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Shell;

// Product decision 55: a switch that sends the conversation somewhere new, or into a smaller context window, is confirmed
// first. The model is chosen in settings (decision 123), so the switch is confirmed when settings change.
public sealed partial class MainWindow
{
    private bool _isConfirmingSwitch;

    private bool NeedsSwitchConfirmed(ProviderAccount target) =>
        _agent is { HasConversation: true } && _provider is { } current && SwitchWarnings(current, target).Count > 0;

    private async Task ConfirmSwitchAsync(ProviderAccount target, string? key, string signature)
    {
        if (_isConfirmingSwitch || _provider is not { } current)
        {
            return;
        }

        _isConfirmingSwitch = true;
        try
        {
            var answer = await ChoiceWindow.AskAsync(
                this,
                Strings.SwitchTitle,
                string.Join("\n\n", SwitchWarnings(current, target)),
                details: null,
                hasDefault: false,
                Strings.Cancel,
                Strings.SwitchConfirm).ConfigureAwait(true);
            if (answer == 1)
            {
                Connect(target, key, signature);
                ApplyModelName();
                return;
            }

            // Back to the model in use, so settings show what the conversation is really sent to.
            _settings.Choose(current.Id, current.Model);
        }
        finally
        {
            _isConfirmingSwitch = false;
        }
    }

    private static List<string> SwitchWarnings(ProviderAccount current, ProviderAccount target)
    {
        var warnings = new List<string>();
        if (current.IsLocal && !target.IsLocal)
        {
            warnings.Add(Strings.Format(Strings.SwitchToCloud, ProviderPrivacy.Destination(target)));
        }

        if (current.ContextWindow is { } from && target.ContextWindow is { } to && to < from)
        {
            warnings.Add(Strings.Format(Strings.SwitchSmallerContext, to, from));
        }

        return warnings;
    }

    // The model the conversation is sent to, by its friendly name, in the agent's header.
    private void ApplyModelName() => _chat.ModelName = _provider is { } provider && _chat.IsConnected ? ProviderCatalog.DisplayName(provider.Model) : null;
}
