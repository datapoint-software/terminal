using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Agent.Workspaces.History;
using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Security.Credentials;
using Datapoint.Terminal.Settings;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Shell;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "A window's lifetime ends with Closed, where they are disposed.")]
public sealed partial class MainWindow : Window, ITerminalOpener, ILocalizable
{
    private readonly ChatViewModel _chat = new(new AvaloniaUiThread());
    private readonly SecretRedactor _redactor = new();
    private readonly WorkspaceWindows _windows;
    private readonly SettingsViewModel _settings;
    private readonly Avalonia.Threading.DispatcherTimer _pendingOutputTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly NativeMenu _recentMenu = [];
    private TerminalAgent? _agent;
    private string? _clientSignature;
    private ProviderAccount? _provider;

    public MainWindow(Workspace workspace, WorkspaceWindows windows)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(windows);
        InitializeComponent();
        Workspace = workspace;
        _windows = windows;
        _settings = windows.Settings;
        _settings.Changed += OnSettingsChanged;
        _settings.AppearanceChanged += OnAppearanceChanged;
        Avalonia.Application.Current?.PlatformSettings?.ColorValuesChanged += OnSystemColorsChanged;
        _settings.WorkspacesChanged += OnWorkspacesChanged;
        WireChat(windows);
        Chat.DataContext = _chat;
        _chat.PropertyChanged += OnChatPropertyChanged;
        DataContext = this;
        Tabs.SelectionChanged += (_, _) => ShowSelectedTab();
        Tabs.AddHandler(PointerReleasedEvent, (_, _) => SelectedTab?.View.Focus(), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        if (_settings.Problem is { } problem)
        {
            _chat.AddNotice(problem);
        }

        foreach (var workspaceProblem in workspace.Problems)
        {
            _chat.AddNotice(workspaceProblem);
        }

        if (workspace.IsReadOnly)
        {
            _chat.AddNotice(Strings.NoticeWorkspaceReadOnly);
        }

        UpdateTitle();
        SetUpAreas();
        SetUpFind();
        SetUpSidebar();
        NativeMenu.SetMenu(this, BuildMenu());
        Localizer.Instance.Register(this);
        LayOutPanes();
        AddHandler(KeyDownEvent, OnShortcutKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Opened += (_, _) => _ = OnOpenedAsync();
        _pendingOutputTimer.Tick += (_, _) => _chat.RefreshPendingOutput();
        _pendingOutputTimer.Start();
        Closing += OnWindowClosing;
        Closed += (_, _) =>
        {
            _pendingOutputTimer.Stop();
            _ = StopAsync();
        };
    }

    public Workspace Workspace { get; }

    // Trust is settled before any terminal opens, so the first conversation already reads the files the right way.
    private async Task OnOpenedAsync()
    {
        ApplySettings();
        await ReviewTrustAsync(always: false).ConfigureAwait(true);
        await ReviewConnectionsAsync(reload: false).ConfigureAwait(true);
        await OpenLocalTabAsync().ConfigureAwait(true);
    }

    public void AddNotice(string text) => _chat.AddNotice(text);

    public static AppSettings FirstRun(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        // Setup offers the key from the environment itself, and adds it if the user sets setup aside.
        if (settings.Providers.Count > 0 || !settings.SetupShown || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            return settings;
        }

        var provider = Setup.SetupViewModel.NewAccount(ProviderKind.Anthropic);
        return settings with { Providers = [provider], SelectedProviderId = provider.Id };
    }

    public static IChatClient CreateClient(ProviderAccount provider, string? key) => ChatClientFactory.Create(provider.ToProviderSettings(), key);

    private void WireChat(WorkspaceWindows windows)
    {
        _chat.SettingsRequested = windows.OpenSettings;
        _chat.PlanShown = ShowPlan;
        _chat.MessageSent = message => Record(HistoryEntryKind.User, message);
        _chat.Cleared = EndConversation;
        _chat.ConfirmIgnorePermissions = ConfirmIgnorePermissionsAsync;
        _chat.WebAlwaysAllowed += (_, _) => _settings.SetAlwaysAllowWeb(true);
        _chat.ReadOnlyApproved = OnReadOnlyApproved;
        _chat.TerminalShown = ShowTerminal;
    }

    private void OnReadOnlyApproved(string command, bool taught)
    {
        if (taught)
        {
            _settings.AddReadOnlyCommand(command);
            return;
        }

        _settings.NoteApproved(command);
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => ApplySettings();

    private void OnAppearanceChanged(object? sender, EventArgs e) => ApplyAppearance();

    // A terminal following the system's light or dark appearance changes with it, whatever the window's own theme.
    private void OnSystemColorsChanged(object? sender, Avalonia.Platform.PlatformColorValues e) => ApplyAppearance();

    private void OnWorkspacesChanged(object? sender, EventArgs e) => FillRecentMenu();

    // A document shown over the terminals names the window, so the title says what's in front of the user.
    private void UpdateTitle() =>
        Title = WindowTitles.For(Workspace.Name, (PlanArea.IsKeyboardFocusWithin ? _planReview : FocusedPane?.Selected)?.Name ?? SelectedTab?.ShellTitle, _chat.NeedsUser);

    private void ApplyLimits(ProviderAccount provider)
    {
        if (_agent is null)
        {
            return;
        }

        _agent.OutputLimit = OutputLimits.For(provider.EffectiveContextWindow, provider.OutputLimit);
        _agent.CompactAtTokens = CompactionLimits.For(provider.EffectiveContextWindow, provider.CompactAt);
    }

    private void ApplySettings()
    {
        var current = _settings.Current;
        var provider = current.SelectedProvider;
        ApplyWebSettings();
        if (provider is not null)
        {
            ApplyLimits(provider);
        }

        var key = ReadKey(provider);
        if (provider is null || !IsUsable(provider, key))
        {
            _chat.IsConnected = false;
            _clientSignature = null;
            ApplyModelName();
            return;
        }

        ApplyUsageSettings(provider);
        var signature = string.Join('|', provider.Id, provider.Kind, provider.Model, provider.Endpoint, key?.GetHashCode(StringComparison.Ordinal));
        if (string.Equals(signature, _clientSignature, StringComparison.Ordinal) && _chat.IsConnected)
        {
            return;
        }

        if (NeedsSwitchConfirmed(provider))
        {
            _ = ConfirmSwitchAsync(provider, key, signature);
            return;
        }

        Connect(provider, key, signature);
        ApplyModelName();
    }

    private void Connect(ProviderAccount provider, string? key, string signature)
    {
        try
        {
            var client = CreateClient(provider, key);
            _redactor.AddKnownSecret(key ?? string.Empty);
            _clientSignature = signature;
            if (_agent is null)
            {
                _agent = new TerminalAgent(client, _chat, _redactor)
                {
                    InstructionsSource = LoadInstructionsAsync,
                    LanguageSource = () => Localizer.Instance.Culture,
                    TerminalOpener = this,
                    IgnoredFileCheck = FindIgnoredFile,
                    Memory = CreateMemory(),
                    HostDocs = CreateHostDocs(),
                    Skills = CreateSkills(),
                    HistorySearch = SearchHistory,
                    HistoryEntryReader = FindHistoryEntry,
                };
                _agent.Mode = _settings.ModeFor(Workspace.Folder);
                _agent.ModeChanged += OnAgentModeChanged;
                _agent.EventRaised += OnMemoryEvent;
                ApplyLimits(provider);
                _agent.EventRaised += OnAgentEvent;
                _agent.EventRaised += OnHistoryEvent;
                _agent.EventRaised += OnRecordingEvent;
                ApplyUsageSettings(provider);
                foreach (var tab in _tabs.Where(tab => tab.AgentTerminal is not null))
                {
                    _agent.AddTerminal(tab.AgentTerminal!);
                }

                _chat.Attach(_agent, ProviderPrivacy.Describe(provider));
            }
            else
            {
                _agent.UseClient(client);
                var isNewDestination = _provider is null || !string.Equals(ProviderPrivacy.Destination(_provider), ProviderPrivacy.Destination(provider), StringComparison.Ordinal);
                _chat.IsConnected = true;
                if (isNewDestination)
                {
                    _chat.AddNotice(Strings.Format(Strings.NoticeSwitched, ProviderPrivacy.Destination(provider)));
                }
            }

            _provider = provider;
            _client = client;
            ApplyWebSettings();
        }
        catch (ArgumentException exception)
        {
            _chat.IsConnected = false;
            _chat.AddNotice(Strings.Format(Strings.NoticeProviderFailed, exception.Message));
        }
    }

    private string? ReadKey(ProviderAccount? provider)
    {
        if (provider is null)
        {
            return null;
        }

        try
        {
            return _settings.Keys.Read(provider);
        }
        catch (CredentialStoreException exception)
        {
            _chat.AddNotice(exception.Message);
            return null;
        }
    }

    private static bool IsUsable(ProviderAccount provider, string? key) =>
        !string.IsNullOrWhiteSpace(provider.Model)
        && (!ProviderCatalog.NeedsKey(provider.Kind) || !string.IsNullOrEmpty(key))
        && (provider.Kind != ProviderKind.OpenAICompatible || Uri.TryCreate(provider.Endpoint, UriKind.Absolute, out _));

    private async Task StopAsync()
    {
        _settings.Changed -= OnSettingsChanged;
        _settings.AreaLayoutChanged -= OnAreaLayoutChanged;
        _settings.AppearanceChanged -= OnAppearanceChanged;
        Avalonia.Application.Current?.PlatformSettings?.ColorValuesChanged -= OnSystemColorsChanged;
        _settings.WorkspacesChanged -= OnWorkspacesChanged;
        _agent?.Dispose();
        _chat.Dispose();
        CloseHistory();
        foreach (var tab in _tabs.ToList())
        {
            await DisposeTabAsync(tab).ConfigureAwait(true);
        }

        Workspace.Dispose();
    }
}
