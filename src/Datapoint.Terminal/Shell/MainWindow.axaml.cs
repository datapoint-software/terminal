using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Security.Credentials;
using Datapoint.Terminal.Sessions;
using Datapoint.Terminal.Sessions.Local;
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
    private TerminalConnection? _connection;
    private TerminalAgent? _agent;
    private string? _clientSignature;
    private ProviderAccount? _provider;
    private string _shellTitle = string.Empty;

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
        _chat.ModelChosen = choice => _settings.Choose(choice.ProviderId, choice.Model);
        Chat.DataContext = _chat;
        if (_settings.Problem is { } problem)
        {
            _chat.AddNotice(problem);
        }

        if (project.IsReadOnly)
        {
            _chat.AddNotice(Strings.NoticeProjectReadOnly);
        }

        UpdateTitle();
        NativeMenu.SetMenu(this, BuildMenu());
        Opened += (_, _) => _ = StartLocalShellAsync();
        Closed += (_, _) => _ = StopAsync();
    }

    public Project Project { get; }

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

    private NativeMenu BuildMenu()
    {
        var shortcut = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        var file = new NativeMenu();
        file.Add(Item(Strings.MenuNewProject, new KeyGesture(Key.N, shortcut | KeyModifiers.Shift), () => _ = _windows.PickAsync(this, isNew: true)));
        file.Add(Item(Strings.MenuOpenProject, new KeyGesture(Key.O, shortcut), () => _ = _windows.PickAsync(this, isNew: false)));
        FillRecentMenu();
        file.Add(new NativeMenuItem(Strings.MenuOpenRecent) { Menu = _recentMenu });
        file.Add(new NativeMenuItemSeparator());
        if (!OperatingSystem.IsMacOS())
        {
            file.Add(Item(Strings.MenuSettings, new KeyGesture(Key.OemComma, shortcut), _windows.OpenSettings));
            file.Add(new NativeMenuItemSeparator());
        }

        file.Add(Item(Strings.MenuCloseWindow, new KeyGesture(Key.W, shortcut | KeyModifiers.Shift), Close));
        return [new NativeMenuItem(Strings.MenuFile) { Menu = file }];
    }

    private void FillRecentMenu()
    {
        var menu = _recentMenu;
        menu.Items.Clear();
        foreach (var folder in _windows.Recent)
        {
            menu.Add(Item(ShortPath(folder), gesture: null, () => _windows.Open(folder)));
        }

        if (_windows.Recent.Count > 0)
        {
            menu.Add(new NativeMenuItemSeparator());
        }

        var clear = Item(Strings.MenuClearRecent, gesture: null, _windows.ClearRecent);
        clear.IsEnabled = _windows.Recent.Count > 0;
        menu.Add(clear);
    }

    private NativeMenuItem Item(string header, KeyGesture? gesture, Action action)
    {
        var item = new NativeMenuItem(header) { Gesture = gesture };
        item.Click += (_, _) => action();
        if (gesture is not null && !OperatingSystem.IsMacOS() && !KeyBindings.Any(binding => gesture.Equals(binding.Gesture)))
        {
            KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = new RelayCommand(action) });
        }

        return item;
    }

    private static string ShortPath(string folder)
    {
        var home = ProjectFolders.Home;
        return folder.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? "~" + folder[home.Length..] : folder;
    }

    private void UpdateTitle() => Title = string.IsNullOrWhiteSpace(_shellTitle)
        ? Strings.Format(Strings.WindowTitle, Project.Name)
        : Strings.Format(Strings.WindowTitle, Strings.Format(Strings.WindowTitleWithShell, Project.Name, _shellTitle));

    private async Task StartLocalShellAsync()
    {
        try
        {
            var options = Project.IsDefault ? new LocalSessionOptions() : new LocalSessionOptions { WorkingDirectory = Project.Folder };
            var session = await LocalSession.StartAsync(options, CancellationToken.None).ConfigureAwait(true);
            var connection = new TerminalConnection(session, 80, 24);
            _connection = connection;
            connection.OutputArrived += (_, _) => Terminal.NotifyOutput();
            connection.TitleChanged += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                _shellTitle = connection.Terminal.Title;
                UpdateTitle();
            });
            connection.Ended += (_, _) => Dispatcher.UIThread.Post(Close);
            Terminal.Input += (_, e) =>
            {
                _chat.OnUserTyped();
                connection.Send(e.Data);
            };
            Terminal.Resized += (_, e) => _ = connection.ResizeAsync(e.Columns, e.Rows, CancellationToken.None);
            Terminal.Terminal = connection.Terminal;
            Terminal.Focus();
            ApplySettings();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            Status.Text = $"The shell couldn't start: {exception.Message}";
            Status.IsVisible = true;
        }
    }

    /// <summary>Follows the settings: the provider and model the agent uses, and the models the chat offers.</summary>
    private void ApplySettings()
    {
        var current = _settings.Current;
        var provider = current.SelectedProvider;
        _chat.SetModels(ModelChoices(current), provider is null ? null : Choice(provider, provider.Model, current));
        if (_connection is null)
        {
            return;
        }

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

        Connect(provider, key, current.DefaultApprovalMode, signature);
    }

    private void Connect(ProviderAccount provider, string? key, ApprovalMode defaultMode, string signature)
    {
        try
        {
            var client = CreateClient(provider, key);
            _redactor.AddKnownSecret(key ?? string.Empty);
            _clientSignature = signature;
            if (_agent is null)
            {
                _agent = new TerminalAgent(client, new ConnectionTerminal(_connection!), _chat, _redactor, new AgentOptions { DefaultMode = defaultMode });
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
        Project.Dispose();
        _chat.Dispose();
        if (_connection is null)
        {
            return;
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
