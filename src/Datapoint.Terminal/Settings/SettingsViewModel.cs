using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Workspaces;
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
        RecentWorkspaces = settings.RecentWorkspaces;
        OpenWorkspaces = settings.OpenWorkspaces;
        TrustedWorkspaces = settings.TrustedWorkspaces;
        DeclinedGitIgnore = settings.DeclinedGitIgnore;
        ConfirmedConnections = settings.ConfirmedConnections;
        WarnedSyncFolders = settings.WarnedSyncFolders;
        NotSecrets = settings.NotSecrets;
        HistoryDays = settings.HistoryDays;
        ChatSide = settings.ChatSide;
        Notifications = settings.Notifications;
        RecordSessions = settings.RecordSessions;
        SpendingLimit = settings.SpendingLimit;
        TerminalTheme = settings.TerminalTheme;
        TerminalFont = settings.TerminalFont;
        Themes = settings.Themes;
        CustomThemeId = settings.Themes.Any(theme => string.Equals(theme.Id, settings.CustomThemeId, StringComparison.Ordinal)) ? settings.CustomThemeId : null;
        MatchWindowToTheme = settings.MatchWindowToTheme;
        TerminalFontSize = settings.TerminalFontSize is >= 8 and <= 40 ? settings.TerminalFontSize : 14;
        SelectedProviderId = settings.SelectedProvider?.Id;
        foreach (var account in settings.Providers)
        {
            Providers.Add(new ProviderViewModel(this, account));
        }

        Selected = Providers.FirstOrDefault(provider => provider.IsActive) ?? Providers.FirstOrDefault();
        _isLoading = false;
    }

    public event EventHandler? Changed;

    public event EventHandler? WorkspacesChanged;

    public ProviderKeys Keys { get; }

    public IReadOnlyList<string> RecentWorkspaces { get; private set; }

    public IReadOnlyList<string> OpenWorkspaces { get; private set; }

    public IReadOnlyList<WorkspaceTrustRecord> TrustedWorkspaces { get; private set; }

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
        SaveWorkspaceData();
    }

    public bool RecordSessions { get; private set; }

    public void SetRecordSessions(bool enabled)
    {
        RecordSessions = enabled;
        SaveWorkspaceData();
    }

    public decimal? SpendingLimit { get; private set; }

    public void SetSpendingLimit(decimal? limit)
    {
        SpendingLimit = limit;
        SaveWorkspaceData();
    }

    public string? TerminalFont { get; private set; }

    public double TerminalFontSize { get; private set; }

    public event EventHandler? AppearanceChanged;

    public IReadOnlyList<Themes.ColorTheme> Themes { get; private set; }

    public string? CustomThemeId { get; private set; }

    public bool MatchWindowToTheme { get; private set; }

    // The colours terminals use now: a chosen custom theme, or the built-in light or dark one.
    public Themes.ColorTheme ActiveTheme(bool systemIsDark) =>
        Themes.FirstOrDefault(theme => string.Equals(theme.Id, CustomThemeId, StringComparison.Ordinal))
        ?? (Shell.TerminalThemes.IsDark(TerminalTheme, systemIsDark) ? Datapoint.Terminal.Themes.BuiltInThemes.Dark : Datapoint.Terminal.Themes.BuiltInThemes.Light);

    public void UseTheme(Shell.TerminalTheme theme)
    {
        CustomThemeId = null;
        SetAppearance(theme, TerminalFont, TerminalFontSize);
    }

    public void UseCustomTheme(string id)
    {
        CustomThemeId = Themes.Any(theme => string.Equals(theme.Id, id, StringComparison.Ordinal)) ? id : null;
        SetAppearance(TerminalTheme, TerminalFont, TerminalFontSize);
    }

    // Adds the theme, or replaces the one with its id, and uses it.
    public void SaveTheme(Themes.ColorTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var index = Themes.ToList().FindIndex(item => string.Equals(item.Id, theme.Id, StringComparison.Ordinal));
        Themes = index < 0 ? [.. Themes, theme] : [.. Themes.Take(index), theme, .. Themes.Skip(index + 1)];
        UseCustomTheme(theme.Id);
    }

    public void RemoveTheme(string id)
    {
        Themes = [.. Themes.Where(theme => !string.Equals(theme.Id, id, StringComparison.Ordinal))];
        if (string.Equals(CustomThemeId, id, StringComparison.Ordinal))
        {
            CustomThemeId = null;
        }

        SetAppearance(TerminalTheme, TerminalFont, TerminalFontSize);
    }

    public void SetMatchWindowToTheme(bool match)
    {
        MatchWindowToTheme = match;
        SetAppearance(TerminalTheme, TerminalFont, TerminalFontSize);
    }

    public void SetAppearance(Shell.TerminalTheme theme, string? font, double size)
    {
        TerminalTheme = theme;
        TerminalFont = font;
        TerminalFontSize = Math.Clamp(size, 8, 40);
        SaveWorkspaceData();
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
        RecentWorkspaces = RecentWorkspaces,
        OpenWorkspaces = OpenWorkspaces,
        TrustedWorkspaces = TrustedWorkspaces,
        DeclinedGitIgnore = DeclinedGitIgnore,
        ConfirmedConnections = ConfirmedConnections,
        WarnedSyncFolders = WarnedSyncFolders,
        NotSecrets = NotSecrets,
        HistoryDays = HistoryDays,
        ChatSide = ChatSide,
        Notifications = Notifications,
        RecordSessions = RecordSessions,
        SpendingLimit = SpendingLimit,
        TerminalTheme = TerminalTheme,
        TerminalFont = TerminalFont,
        TerminalFontSize = TerminalFontSize,
        Themes = Themes,
        CustomThemeId = CustomThemeId,
        MatchWindowToTheme = MatchWindowToTheme,
    };

    public IReadOnlyList<WorkspaceConnection>? ConfirmedFor(string folder) =>
        ConfirmedConnections.FirstOrDefault(record => WorkspaceFolders.AreSame(record.Folder, folder))?.Connections;

    public void SetConfirmed(string folder, IReadOnlyList<WorkspaceConnection> connections)
    {
        var record = new ConfirmedConnections { Folder = folder, Connections = connections };
        ConfirmedConnections = [.. ConfirmedConnections.Where(item => !WorkspaceFolders.AreSame(item.Folder, folder)), record];
        SaveWorkspaceData();
    }

    public bool HasDeclinedGitIgnore(string folder) => DeclinedGitIgnore.Any(item => WorkspaceFolders.AreSame(item, folder));

    public void DeclineGitIgnore(string folder)
    {
        DeclinedGitIgnore = [.. DeclinedGitIgnore.Where(item => !WorkspaceFolders.AreSame(item, folder)), folder];
        SaveWorkspaceData();
    }

    public bool HasWarnedSyncFolder(string folder) => WarnedSyncFolders.Any(item => WorkspaceFolders.AreSame(item, folder));

    public void WarnedSyncFolder(string folder)
    {
        WarnedSyncFolders = [.. WarnedSyncFolders.Where(item => !WorkspaceFolders.AreSame(item, folder)), folder];
        SaveWorkspaceData();
    }

    // Read from the agent's thread too; the list is replaced, never changed in place.
    public bool IsNotSecret(string value) => NotSecrets.Contains(SecretRedactor.Fingerprint(value), StringComparer.Ordinal);

    public void MarkNotSecret(IEnumerable<string> values)
    {
        NotSecrets = [.. NotSecrets.Union(values.Select(SecretRedactor.Fingerprint), StringComparer.Ordinal)];
        SaveWorkspaceData();
    }

    public void SetChatSide(Shell.ChatSide side)
    {
        ChatSide = side;
        SaveWorkspaceData();
    }

    public void SetHistoryDays(int? days)
    {
        HistoryDays = days;
        SaveWorkspaceData();
    }

    public WorkspaceTrustRecord? TrustFor(string folder) =>
        TrustedWorkspaces.FirstOrDefault(record => WorkspaceFolders.AreSame(record.Folder, folder));

    public void SetTrust(WorkspaceTrustRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        TrustedWorkspaces = [.. TrustedWorkspaces.Where(item => !WorkspaceFolders.AreSame(item.Folder, record.Folder)), record];
        SaveWorkspaceData();
    }

    // Saved without raising Changed: nothing a window shows depends on it.
    private void SaveWorkspaceData()
    {
        if (_store.IsReadOnly)
        {
            return;
        }

        _store.Save(Current);
    }

    public void SetWorkspaces(IReadOnlyList<string> recent, IReadOnlyList<string> open)
    {
        RecentWorkspaces = recent;
        OpenWorkspaces = open;
        if (!_store.IsReadOnly)
        {
            _store.Save(Current);
        }

        WorkspacesChanged?.Invoke(this, EventArgs.Empty);
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
