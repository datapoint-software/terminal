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
        WebKeys = new WebSearchKeys(keys.Store, Environment.GetEnvironmentVariable);
        LoginPasswords = new LoginPasswords(keys.Store);
        Kinds =
        [
            new(ProviderKind.Anthropic, Strings.ProviderKindAnthropic),
            new(ProviderKind.OpenAI, Strings.ProviderKindOpenAI),
            new(ProviderKind.OpenAICompatible, Strings.ProviderKindCompatible),
        ];
        WorkspaceModes = settings.WorkspaceModes;
        RecentWorkspaces = settings.RecentWorkspaces;
        OpenWorkspaces = settings.OpenWorkspaces;
        TrustedWorkspaces = settings.TrustedWorkspaces;
        ConfirmedConnections = settings.ConfirmedConnections;
        NotSecrets = settings.NotSecrets;
        LearnedCommands = LearnedCommands.From(settings.ReadOnlyCommands);
        SavedLogins = settings.SavedLogins;
        HistoryDays = settings.HistoryDays;
        (SetupShown, TipsPending) = (settings.SetupShown, settings.TipsPending);
        TerminalSide = settings.TerminalSide;
        AreaLayout = settings.AreaLayout;
        Notifications = settings.Notifications;
        RecordSessions = settings.RecordSessions;
        SpendingLimit = settings.SpendingLimit;
        TerminalTheme = settings.TerminalTheme;
        TerminalFont = settings.TerminalFont;
        Themes = settings.Themes;
        CustomThemeId = settings.Themes.Any(theme => string.Equals(theme.Id, settings.CustomThemeId, StringComparison.Ordinal)) ? settings.CustomThemeId : null;
        WindowTheme = settings.WindowTheme is { } windowTheme && Enum.IsDefined(windowTheme) ? windowTheme
            : settings.MatchWindowToTheme ? Datapoint.Terminal.Themes.WindowTheme.Terminal
            : Datapoint.Terminal.Themes.WindowTheme.System;
        ReduceMotion = settings.ReduceMotion;
        AlwaysAllowWeb = settings.AlwaysAllowWeb;
        WebSearch = settings.WebSearch;
        SearxngServer = settings.SearxngServer;
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

    public IReadOnlyList<ConfirmedConnections> ConfirmedConnections { get; private set; }

    public IReadOnlyList<WorkspaceMode> WorkspaceModes { get; private set; }

    public IReadOnlyList<string> NotSecrets { get; private set; }

    public int? HistoryDays { get; private set; }

    public Shell.TerminalSide TerminalSide { get; private set; }

    public bool SetupShown { get; private set; }

    public bool TipsPending { get; private set; }

    // Setup is offered once, to an installation with no provider.
    public bool NeedsSetup => !SetupShown && Providers.Count == 0;

    public void MarkSetupShown()
    {
        SetupShown = true;
        TipsPending = true;
        SaveWorkspaceData();
    }

    public void SetTipsPending(bool pending)
    {
        TipsPending = pending;
        SaveWorkspaceData();
    }

    // Adds the provider and uses it, as setup does once the user has chosen one.
    public ProviderViewModel AddProvider(ProviderAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var provider = new ProviderViewModel(this, account);
        Providers.Add(provider);
        Selected = provider;
        OnPropertyChanged(nameof(IsEmpty));
        Activate(provider);
        return provider;
    }

    public string? AreaLayout { get; private set; }

    // Every window lays out its areas the same way, so a move in one is made in the others.
    public event EventHandler? AreaLayoutChanged;

    public void SetAreaLayout(string layout)
    {
        if (string.Equals(layout, AreaLayout, StringComparison.Ordinal))
        {
            return;
        }

        AreaLayout = layout;
        SaveWorkspaceData();
        AreaLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

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

    public Datapoint.Terminal.Themes.WindowTheme WindowTheme { get; private set; }

    public bool ReduceMotion { get; private set; }

    public bool AlwaysAllowWeb { get; private set; }

    public WebSearchService WebSearch { get; private set; }

    public string? SearxngServer { get; private set; }

    public WebSearchKeys WebKeys { get; }

    public void SetWebSearch(WebSearchService service, string? searxngServer)
    {
        WebSearch = service;
        SearxngServer = string.IsNullOrWhiteSpace(searxngServer) ? null : searxngServer.Trim();
        Save();
    }

    public LoginPasswords LoginPasswords { get; }

    public IReadOnlyList<SavedLogin> SavedLogins { get; private set; }

    public SavedLogin? SavedLoginFor(string login) =>
        SavedLogins.FirstOrDefault(item => string.Equals(item.Login, login, StringComparison.Ordinal));

    // Keeps the password in the credential store, replacing any saved before; it's never kept in the settings file.
    public void SaveLoginPassword(string login, string password)
    {
        LoginPasswords.Save(login, password);
        var usesWithoutAsking = SavedLoginFor(login)?.UsesWithoutAsking == true;
        SavedLogins = [.. SavedLogins.Where(item => !string.Equals(item.Login, login, StringComparison.Ordinal)), new SavedLogin { Login = login, UsesWithoutAsking = usesWithoutAsking }];
        Save();
    }

    public void SetUsesWithoutAsking(string login, bool usesWithoutAsking)
    {
        if (SavedLoginFor(login) is not { } saved || saved.UsesWithoutAsking == usesWithoutAsking)
        {
            return;
        }

        SavedLogins = [.. SavedLogins.Select(item => ReferenceEquals(item, saved) ? item with { UsesWithoutAsking = usesWithoutAsking } : item)];
        Save();
    }

    public void ForgetLogin(string login)
    {
        LoginPasswords.Delete(login);
        SavedLogins = [.. SavedLogins.Where(item => !string.Equals(item.Login, login, StringComparison.Ordinal))];
        Save();
    }

    // Replaced whole when it changes, and every window's agent follows it.
    public LearnedCommands LearnedCommands { get; private set; }

    // Commands the check counted as changes that the user approved in this session, most recent first, as they could be
    // taught. Never saved: the list is only a shortcut for teaching them.
    public IReadOnlyList<string> RecentlyApproved { get; private set; } = [];

    public const int MaxRecentlyApproved = 20;

    // False when the command can't be taught, e.g. because it runs other commands.
    public bool AddReadOnlyCommand(string command)
    {
        if (LearnedCommands.Normalize(command) is not { } normalized)
        {
            return false;
        }

        if (LearnedCommands.Commands.Contains(normalized, StringComparer.Ordinal))
        {
            return true;
        }

        LearnedCommands = LearnedCommands.From([.. LearnedCommands.Commands, normalized]);
        Save();
        return true;
    }

    public void RemoveReadOnlyCommand(string command)
    {
        if (!LearnedCommands.Commands.Contains(command, StringComparer.Ordinal))
        {
            return;
        }

        LearnedCommands = LearnedCommands.From(LearnedCommands.Commands.Where(item => !string.Equals(item, command, StringComparison.Ordinal)));
        Save();
    }

    public void NoteApproved(string command)
    {
        RecentlyApproved = [.. RecentlyApproved.Where(item => !string.Equals(item, command, StringComparison.Ordinal)).Prepend(command).Take(MaxRecentlyApproved)];
        RecentlyApprovedChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? RecentlyApprovedChanged;

    // Every window's agent follows it, so allowing or taking it back applies everywhere at once.
    public void SetAlwaysAllowWeb(bool allow)
    {
        if (AlwaysAllowWeb == allow)
        {
            return;
        }

        AlwaysAllowWeb = allow;
        Save();
    }

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

    public void SetWindowTheme(Datapoint.Terminal.Themes.WindowTheme theme)
    {
        WindowTheme = theme;
        SetAppearance(TerminalTheme, TerminalFont, TerminalFontSize);
    }

    public void SetReduceMotion(bool reduce)
    {
        ReduceMotion = reduce;
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
        WorkspaceModes = WorkspaceModes,
        RecentWorkspaces = RecentWorkspaces,
        OpenWorkspaces = OpenWorkspaces,
        TrustedWorkspaces = TrustedWorkspaces,
        ConfirmedConnections = ConfirmedConnections,
        NotSecrets = NotSecrets,
        ReadOnlyCommands = LearnedCommands.Commands,
        SavedLogins = SavedLogins,
        HistoryDays = HistoryDays,
        TerminalSide = TerminalSide,
        AreaLayout = AreaLayout,
        Notifications = Notifications,
        RecordSessions = RecordSessions,
        SpendingLimit = SpendingLimit,
        TerminalTheme = TerminalTheme,
        TerminalFont = TerminalFont,
        TerminalFontSize = TerminalFontSize,
        Themes = Themes,
        CustomThemeId = CustomThemeId,
        WindowTheme = WindowTheme,
        MatchWindowToTheme = WindowTheme == Datapoint.Terminal.Themes.WindowTheme.Terminal,
        ReduceMotion = ReduceMotion,
        AlwaysAllowWeb = AlwaysAllowWeb,
        WebSearch = WebSearch,
        SearxngServer = SearxngServer,
        SetupShown = SetupShown,
        TipsPending = TipsPending,
    };

    public IReadOnlyList<WorkspaceConnection>? ConfirmedFor(string folder) =>
        ConfirmedConnections.FirstOrDefault(record => WorkspaceFolders.AreSame(record.Folder, folder))?.Connections;

    public ApprovalMode ModeFor(string folder) =>
        WorkspaceModes.FirstOrDefault(record => WorkspaceFolders.AreSame(record.Folder, folder))?.Mode ?? ApprovalMode.Ask;

    // Ignore permissions is never kept; the agent passes the mode before it instead.
    public void SetMode(string folder, ApprovalMode mode)
    {
        if (mode == ApprovalMode.IgnorePermissions || ModeFor(folder) == mode)
        {
            return;
        }

        WorkspaceModes = [.. WorkspaceModes.Where(item => !WorkspaceFolders.AreSame(item.Folder, folder)), new WorkspaceMode { Folder = folder, Mode = mode }];
        SaveWorkspaceData();
    }

    public void SetConfirmed(string folder, IReadOnlyList<WorkspaceConnection> connections)
    {
        var record = new ConfirmedConnections { Folder = folder, Connections = connections };
        ConfirmedConnections = [.. ConfirmedConnections.Where(item => !WorkspaceFolders.AreSame(item.Folder, folder)), record];
        SaveWorkspaceData();
    }

    // Read from the agent's thread too; the list is replaced, never changed in place.
    public bool IsNotSecret(string value) => NotSecrets.Contains(SecretRedactor.Fingerprint(value), StringComparer.Ordinal);

    public void MarkNotSecret(IEnumerable<string> values)
    {
        NotSecrets = [.. NotSecrets.Union(values.Select(SecretRedactor.Fingerprint), StringComparer.Ordinal)];
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
