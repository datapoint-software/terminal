using System.Globalization;
using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Shell;

// Product decision 49: prices come from the provider's settings and the limit from the user; both apply from the next request.
public sealed partial class MainWindow
{
    private void ApplyUsageSettings(ProviderAccount provider)
    {
        if (_agent is null)
        {
            return;
        }

        _agent.Usage.Price = provider.Price;
        _chat.ContextWindow = provider.EffectiveContextWindow;
        _agent.Usage.Budget = _settings.SpendingLimit;
        _chat.RefreshUsage();
    }

    private async Task SetSpendingLimitAsync()
    {
        var current = _settings.SpendingLimit?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        var message = _agent?.Usage.Price is null ? Strings.SpendingLimitMessage + " " + Strings.SpendingLimitNeedsPrices : Strings.SpendingLimitMessage;
        var answers = await PromptWindow.AskAsync(this, Strings.SpendingLimitTitle, message, serverText: null, [(Strings.SpendingLimitLabel, false)], [current]).ConfigureAwait(true);
        if (answers is null)
        {
            return;
        }

        var text = answers[0].Trim();
        var value = text.Length == 0 ? null : ProviderViewModel.Price(text);
        if (text.Length > 0 && value is null)
        {
            _chat.AddNotice(Strings.NoticeSpendingLimitUnreadable);
            return;
        }

        ApplySpendingLimit(value);
    }

    private void ApplySpendingLimit(decimal? value)
    {
        _settings.SetSpendingLimit(value);
        if (_agent is not null)
        {
            _agent.Usage.Budget = value;
        }

        _chat.RefreshUsage();
        _chat.AddNotice(value is { } set ? Strings.Format(Strings.NoticeSpendingLimitSet, UsageText.Money(set)) : Strings.NoticeSpendingLimitRemoved);
    }
}
