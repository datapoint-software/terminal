using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Shell;

// Product decision 55: a switch that sends the conversation somewhere new, or into a smaller context window, is confirmed first.
public sealed partial class MainWindow
{
    private async Task<bool> ConfirmAutoAsync(Agent.AgentTerminal terminal)
    {
        var answer = await ChoiceWindow.AskAsync(
            this,
            Strings.Format(Strings.AutoProductionTitle, terminal.Name),
            Strings.Format(Strings.AutoProductionMessage, terminal.Environment),
            details: null,
            hasDefault: false,
            Strings.Cancel,
            Strings.AutoProductionConfirm).ConfigureAwait(true);
        return answer == 1;
    }

    private async Task ChooseModelAsync(ModelChoice choice)
    {
        var target = _settings.Current.Providers.FirstOrDefault(item => string.Equals(item.Id, choice.ProviderId, StringComparison.Ordinal));
        if (target is null || _agent is not { HasConversation: true } || _provider is not { } current
            || await ConfirmSwitchAsync(current, target).ConfigureAwait(true))
        {
            _settings.Choose(choice.ProviderId, choice.Model);
            return;
        }

        // Put the picker back on the model in use.
        ApplySettings();
    }

    private async Task<bool> ConfirmSwitchAsync(ProviderAccount current, ProviderAccount target)
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

        if (warnings.Count == 0)
        {
            return true;
        }

        var answer = await ChoiceWindow.AskAsync(
            this,
            Strings.SwitchTitle,
            string.Join("\n\n", warnings),
            details: null,
            hasDefault: false,
            Strings.Cancel,
            Strings.SwitchConfirm).ConfigureAwait(true);
        return answer == 1;
    }
}
