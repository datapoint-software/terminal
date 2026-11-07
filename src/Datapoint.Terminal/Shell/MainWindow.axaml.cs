using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Agent.Projects.History;
using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Security.Credentials;
using Datapoint.Terminal.Settings;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Shell;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "A window's lifetime ends with Closed, where they are disposed.")]
public sealed partial class MainWindow : Window
{
    private readonly ChatViewModel _chat = new(new AvaloniaUiThread());
    private readonly SecretRedactor _redactor = new();
    private readonly ProjectWindows _windows;
    private readonly SettingsViewModel _settings;
    private readonly NativeMenu _recentMenu = [];
    private TerminalAgent? _agent;
    private string? _clientSignature;
    private ProviderAccount? _provider;

    public MainWindow(Project project, ProjectWindows windows)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(windows);
        InitializeComponent();
        Project = project;
        _windows = windows;
        _settings = windows.Settings;
        _settings.Changed += OnSettingsChanged;
        _settings.ProjectsChanged += OnProjectsChanged;
        _chat.SettingsRequested = windows.OpenSettings;
        _chat.MessageSent = message => Record(HistoryEntryKind.User, message);
        _chat.Cleared = EndConversation;
        _chat.ModelChosen = choice => _settings.Choose(choice.ProviderId, choice.Model);
        Chat.DataContext = _chat;
        _chat.PropertyChanged += OnChatPropertyChanged;
        DataContext = this;
        Tabs.SelectionChanged += (_, _) => ShowSelectedTab();
        Tabs.AddHandler(PointerReleasedEvent, (_, _) => SelectedTab?.View.Focus(), Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        if (_settings.Problem is { } problem)
        {
            _chat.AddNotice(problem);
        }

        foreach (var projectProblem in project.Problems)
        {
            _chat.AddNotice(projectProblem);
        }

        if (project.IsReadOnly)
        {
            _chat.AddNotice(Strings.NoticeProjectReadOnly);
        }

        UpdateTitle();
        NativeMenu.SetMenu(this, BuildMenu());
        AddHandler(KeyDownEvent, OnShortcutKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Opened += (_, _) => _ = OnOpenedAsync();
        Closing += OnWindowClosing;
        Closed += (_, _) => _ = StopAsync();
    }

    public Project Project { get; }

    // Trust is settled before any terminal opens, so the first conversation already reads the files the right way.
    private async Task OnOpenedAsync()
    {
        ApplySettings();
        await ReviewTrustAsync(always: false).ConfigureAwait(true);
        await ReviewConnectionsAsync(reload: false).ConfigureAwait(true);
        await OfferGitIgnoreAsync().ConfigureAwait(true);
        WarnAboutSyncFolder();
        await OpenLocalTabAsync().ConfigureAwait(true);
    }

    public void AddNotice(string text) => _chat.AddNotice(text);

    public static AppSettings FirstRun(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Providers.Count > 0 || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            return settings;
        }

        var provider = new ProviderAccount
        {
            Id = ProviderAccount.NewId(),
            Kind = ProviderKind.Anthropic,
            Name = ProviderCatalog.DefaultName(ProviderKind.Anthropic),
            Model = ProviderCatalog.DefaultModel(ProviderKind.Anthropic),
        };
        return settings with { Providers = [provider], SelectedProviderId = provider.Id };
    }

    public static IChatClient CreateClient(ProviderAccount provider, string? key) => ChatClientFactory.Create(provider.ToProviderSettings(), key);

    private void OnSettingsChanged(object? sender, EventArgs e) => ApplySettings();

    private void OnProjectsChanged(object? sender, EventArgs e) => FillRecentMenu();

    private void UpdateTitle() => Title = WindowTitles.For(Project.Name, SelectedTab?.ShellTitle, _chat.NeedsUser);

    private void ApplySettings()
    {
        var current = _settings.Current;
        var provider = current.SelectedProvider;
        _chat.SetModels(ModelChoices(current), provider is null ? null : Choice(provider, provider.Model, current));
        var key = ReadKey(provider);
        if (provider is null || !IsUsable(provider, key))
        {
            _chat.IsConnected = false;
            _clientSignature = null;
            return;
        }

        var signature = string.Join('|', provider.Id, provider.Kind, provider.Model, provider.Endpoint, key?.GetHashCode(StringComparison.Ordinal));
        if (string.Equals(signature, _clientSignature, StringComparison.Ordinal) && _chat.IsConnected)
        {
            return;
        }

        Connect(provider, key, signature);
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
                _agent = new TerminalAgent(client, _chat, _redactor) { InstructionsSource = LoadInstructionsAsync };
                _agent.EventRaised += OnAgentEvent;
                _agent.EventRaised += OnHistoryEvent;
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

    private static IEnumerable<ModelChoice> ModelChoices(AppSettings settings) =>
        settings.Providers.SelectMany(provider =>
            ProviderCatalog.Models(provider.Kind).Append(provider.Model)
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.Ordinal)
                .Select(model => Choice(provider, model, settings)));

    private static ModelChoice Choice(ProviderAccount provider, string model, AppSettings settings) =>
        new(provider.Id, model, ProviderCatalog.DisplayName(model), settings.Providers.Count > 1 ? provider.Name : string.Empty);

    private async Task StopAsync()
    {
        _settings.Changed -= OnSettingsChanged;
        _settings.ProjectsChanged -= OnProjectsChanged;
        _agent?.Dispose();
        _chat.Dispose();
        CloseHistory();
        foreach (var tab in _tabs.ToList())
        {
            await DisposeTabAsync(tab).ConfigureAwait(true);
        }

        Project.Dispose();
    }
}
