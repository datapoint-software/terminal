using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Datapoint.Terminal.Agent;
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
    private readonly ProviderKeys _keys = new(CredentialStores.ForThisSystem(), Environment.GetEnvironmentVariable);
    private readonly SettingsViewModel _settings;
    private SettingsWindow? _settingsWindow;
    private TerminalConnection? _connection;
    private TerminalAgent? _agent;
    private string? _clientSignature;
    private ProviderAccount? _provider;

    public MainWindow()
        : this(new SettingsStore(SettingsStore.DefaultPath))
    {
    }

    public MainWindow(SettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        InitializeComponent();
        _settings = new SettingsViewModel(store, FirstRun(store.Load()), _keys, CreateClient);
        _settings.Changed += (_, _) => ApplySettings();
        _chat.SettingsRequested = OpenSettings;
        _chat.ModelChosen = choice => _settings.Choose(choice.ProviderId, choice.Model);
        Chat.DataContext = _chat;
        if (_settings.Problem is { } problem)
        {
            _chat.AddNotice(problem);
        }

        // On macOS, Settings… is in the application menu (App.axaml) with Cmd+,; elsewhere Ctrl+, opens it.
        if (!OperatingSystem.IsMacOS())
        {
            KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Control), Command = _chat.OpenSettingsCommand });
        }

        Opened += (_, _) => _ = StartLocalShellAsync();
        Closed += (_, _) => _ = StopAsync();
    }

    public SettingsViewModel Settings => _settings;

    public void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow { DataContext = _settings };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show(this);
    }

    /// <summary>
    /// Someone who already sets ANTHROPIC_API_KEY gets an Anthropic provider that uses it, so the agent works on first run;
    /// settings then offer to keep the key in the credential store instead.
    /// </summary>
    private static AppSettings FirstRun(AppSettings settings)
    {
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

    private static IChatClient CreateClient(ProviderAccount provider, string? key) => ChatClientFactory.Create(provider.ToProviderSettings(), key);

    private async Task StartLocalShellAsync()
    {
        try
        {
            var session = await LocalSession.StartAsync(new LocalSessionOptions(), CancellationToken.None).ConfigureAwait(true);
            var connection = new TerminalConnection(session, 80, 24);
            _connection = connection;
            connection.OutputArrived += (_, _) => Terminal.NotifyOutput();
            connection.TitleChanged += (_, _) => Dispatcher.UIThread.Post(() => Title = TitleFor(connection.Terminal.Title));
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
            return _keys.Read(provider);
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
        new(provider.Id, model, settings.Providers.Count > 1 ? $"{model} · {provider.Name}" : model);

    private async Task StopAsync()
    {
        _settingsWindow?.Close();
        _agent?.Dispose();
        _chat.Dispose();
        if (_connection is null)
        {
            return;
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    private static string TitleFor(string shellTitle) =>
        string.IsNullOrWhiteSpace(shellTitle) ? "Datapoint Terminal" : $"{shellTitle} — Datapoint Terminal";
}
