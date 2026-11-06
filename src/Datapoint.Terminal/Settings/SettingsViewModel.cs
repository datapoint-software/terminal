using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Resources;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Settings;

/// <summary>The settings window: for now, the AI providers.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly Func<ProviderAccount, string?, IChatClient> _createClient;
    private ApprovalMode _defaultApprovalMode;
    private bool _isLoading = true;

    public SettingsViewModel(SettingsStore store, AppSettings settings, ProviderKeys keys, Func<ProviderAccount, string?, IChatClient> createClient)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(createClient);
        _store = store;
        _createClient = createClient;
        Keys = keys;
        Kinds =
        [
            new(ProviderKind.Anthropic, Strings.ProviderKindAnthropic),
            new(ProviderKind.OpenAI, Strings.ProviderKindOpenAI),
            new(ProviderKind.OpenAICompatible, Strings.ProviderKindCompatible),
        ];
        _defaultApprovalMode = settings.DefaultApprovalMode;
        SelectedProviderId = settings.SelectedProvider?.Id;
        foreach (var account in settings.Providers)
        {
            Providers.Add(new ProviderViewModel(this, account));
        }

        Selected = Providers.FirstOrDefault(provider => provider.IsActive) ?? Providers.FirstOrDefault();
        _isLoading = false;
    }

    /// <summary>Raised after any change is saved, so the agent can follow it.</summary>
    public event EventHandler? Changed;

    public ProviderKeys Keys { get; }

    public IReadOnlyList<KindOption> Kinds { get; }

    public ObservableCollection<ProviderViewModel> Providers { get; } = [];

    public string? SelectedProviderId { get; private set; }

    public string? Problem => _store.Problem ?? (_store.IsReadOnly ? "A newer version of Datapoint Terminal wrote these settings; changes here won't be saved." : null);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial ProviderViewModel? Selected { get; set; }

    public bool HasSelection => Selected is not null;

    public bool IsEmpty => Providers.Count == 0;

    public AppSettings Current => new()
    {
        Providers = [.. Providers.Select(provider => provider.ToAccount())],
        SelectedProviderId = SelectedProviderId,
        DefaultApprovalMode = _defaultApprovalMode,
    };

    internal IChatClient CreateClient(ProviderAccount account, string? key) => _createClient(account, key);

    internal void Activate(ProviderViewModel provider)
    {
        SelectedProviderId = provider.Id;
        foreach (var item in Providers)
        {
            item.NotifyActiveChanged();
        }

        Save();
    }

    internal void Save()
    {
        if (_isLoading)
        {
            return;
        }

        if (!_store.IsReadOnly)
        {
            _store.Save(Current);
        }

        NotifyChanged();
    }

    internal void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Add()
    {
        var account = new ProviderAccount
        {
            Id = ProviderAccount.NewId(),
            Kind = ProviderKind.Anthropic,
            Name = ProviderCatalog.DefaultName(ProviderKind.Anthropic),
            Model = ProviderCatalog.DefaultModel(ProviderKind.Anthropic),
        };
        var provider = new ProviderViewModel(this, account);
        Providers.Add(provider);
        Selected = provider;
        OnPropertyChanged(nameof(IsEmpty));
        if (SelectedProviderId is null)
        {
            Activate(provider);
            return;
        }

        Save();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        var provider = Selected!;
        Keys.Delete(provider.ToAccount());
        var index = Providers.IndexOf(provider);
        Providers.Remove(provider);
        Selected = Providers.Count == 0 ? null : Providers[Math.Min(index, Providers.Count - 1)];
        OnPropertyChanged(nameof(IsEmpty));
        if (string.Equals(SelectedProviderId, provider.Id, StringComparison.Ordinal))
        {
            SelectedProviderId = Providers.FirstOrDefault()?.Id;
            foreach (var item in Providers)
            {
                item.NotifyActiveChanged();
            }
        }

        Save();
    }
}
