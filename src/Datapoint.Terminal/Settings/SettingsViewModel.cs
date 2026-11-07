using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Resources;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Settings;

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
        RecentProjects = settings.RecentProjects;
        OpenProjects = settings.OpenProjects;
        TrustedProjects = settings.TrustedProjects;
        DeclinedGitIgnore = settings.DeclinedGitIgnore;
        ConfirmedConnections = settings.ConfirmedConnections;
        WarnedSyncFolders = settings.WarnedSyncFolders;
        NotSecrets = settings.NotSecrets;
        HistoryDays = settings.HistoryDays;
        ChatSide = settings.ChatSide;
        Notifications = settings.Notifications;
        TerminalTheme = settings.TerminalTheme;
        TerminalFont = settings.TerminalFont;
        TerminalFontSize = settings.TerminalFontSize;
        SelectedProviderId = settings.SelectedProvider?.Id;
        foreach (var account in settings.Providers)
        {
            Providers.Add(new ProviderViewModel(this, account));
        }

        Selected = Providers.FirstOrDefault(provider => provider.IsActive) ?? Providers.FirstOrDefault();
        _isLoading = false;
    }

    public event EventHandler? Changed;

    public event EventHandler? ProjectsChanged;

    public ProviderKeys Keys { get; }

    public IReadOnlyList<string> RecentProjects { get; private set; }

    public IReadOnlyList<string> OpenProjects { get; private set; }

    public IReadOnlyList<ProjectTrustRecord> TrustedProjects { get; private set; }

    public IReadOnlyList<string> DeclinedGitIgnore { get; private set; }

    public IReadOnlyList<ConfirmedConnections> ConfirmedConnections { get; private set; }

    public IReadOnlyList<string> WarnedSyncFolders { get; private set; }

    public IReadOnlyList<string> NotSecrets { get; private set; }

    public int? HistoryDays { get; private set; }

    public Shell.ChatSide ChatSide { get; private set; }

    public Shell.TerminalTheme TerminalTheme { get; private set; }

    public bool Notifications { get; private set; }

    public void SetNotifications(bool enabled)
    {
        Notifications = enabled;
        SaveProjectData();
    }

    public string? TerminalFont { get; private set; }

    public double TerminalFontSize { get; private set; }

    public event EventHandler? AppearanceChanged;

    public void SetAppearance(Shell.TerminalTheme theme, string? font, double size)
    {
        TerminalTheme = theme;
        TerminalFont = font;
        TerminalFontSize = Math.Clamp(size, 8, 40);
        SaveProjectData();
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

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
        RecentProjects = RecentProjects,
        OpenProjects = OpenProjects,
        TrustedProjects = TrustedProjects,
        DeclinedGitIgnore = DeclinedGitIgnore,
        ConfirmedConnections = ConfirmedConnections,
        WarnedSyncFolders = WarnedSyncFolders,
        NotSecrets = NotSecrets,
        HistoryDays = HistoryDays,
        ChatSide = ChatSide,
        Notifications = Notifications,
        TerminalTheme = TerminalTheme,
        TerminalFont = TerminalFont,
        TerminalFontSize = TerminalFontSize,
    };

    public IReadOnlyList<ProjectConnection>? ConfirmedFor(string folder) =>
        ConfirmedConnections.FirstOrDefault(record => ProjectFolders.AreSame(record.Folder, folder))?.Connections;

    public void SetConfirmed(string folder, IReadOnlyList<ProjectConnection> connections)
    {
        var record = new ConfirmedConnections { Folder = folder, Connections = connections };
        ConfirmedConnections = [.. ConfirmedConnections.Where(item => !ProjectFolders.AreSame(item.Folder, folder)), record];
        SaveProjectData();
    }

    public bool HasDeclinedGitIgnore(string folder) => DeclinedGitIgnore.Any(item => ProjectFolders.AreSame(item, folder));

    public void DeclineGitIgnore(string folder)
    {
        DeclinedGitIgnore = [.. DeclinedGitIgnore.Where(item => !ProjectFolders.AreSame(item, folder)), folder];
        SaveProjectData();
    }

    public bool HasWarnedSyncFolder(string folder) => WarnedSyncFolders.Any(item => ProjectFolders.AreSame(item, folder));

    public void WarnedSyncFolder(string folder)
    {
        WarnedSyncFolders = [.. WarnedSyncFolders.Where(item => !ProjectFolders.AreSame(item, folder)), folder];
        SaveProjectData();
    }

    // Read from the agent's thread too; the list is replaced, never changed in place.
    public bool IsNotSecret(string value) => NotSecrets.Contains(SecretRedactor.Fingerprint(value), StringComparer.Ordinal);

    public void MarkNotSecret(IEnumerable<string> values)
    {
        NotSecrets = [.. NotSecrets.Union(values.Select(SecretRedactor.Fingerprint), StringComparer.Ordinal)];
        SaveProjectData();
    }

    public void SetChatSide(Shell.ChatSide side)
    {
        ChatSide = side;
        SaveProjectData();
    }

    public void SetHistoryDays(int? days)
    {
        HistoryDays = days;
        SaveProjectData();
    }

    public ProjectTrustRecord? TrustFor(string folder) =>
        TrustedProjects.FirstOrDefault(record => ProjectFolders.AreSame(record.Folder, folder));

    public void SetTrust(ProjectTrustRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        TrustedProjects = [.. TrustedProjects.Where(item => !ProjectFolders.AreSame(item.Folder, record.Folder)), record];
        SaveProjectData();
    }

    // Saved without raising Changed: nothing a window shows depends on it.
    private void SaveProjectData()
    {
        if (_store.IsReadOnly)
        {
            return;
        }

        _store.Save(Current);
    }

    public void SetProjects(IReadOnlyList<string> recent, IReadOnlyList<string> open)
    {
        RecentProjects = recent;
        OpenProjects = open;
        if (!_store.IsReadOnly)
        {
            _store.Save(Current);
        }

        ProjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    internal IChatClient CreateClient(ProviderAccount account, string? key) => _createClient(account, key);

    public void Choose(string providerId, string model)
    {
        var provider = Providers.FirstOrDefault(item => string.Equals(item.Id, providerId, StringComparison.Ordinal));
        if (provider is null)
        {
            return;
        }

        _isLoading = true;
        provider.Model = model;
        _isLoading = false;
        Activate(provider);
    }

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
