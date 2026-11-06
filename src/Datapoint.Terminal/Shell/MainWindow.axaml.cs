using Avalonia.Controls;
using Avalonia.Threading;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Chat;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions;
using Datapoint.Terminal.Sessions.Local;

namespace Datapoint.Terminal.Shell;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "A window's lifetime ends with Closed, where they are disposed.")]
public sealed partial class MainWindow : Window
{
    private readonly ChatViewModel _chat = new(new AvaloniaUiThread());
    private TerminalConnection? _connection;
    private TerminalAgent? _agent;

    public MainWindow()
    {
        InitializeComponent();
        Chat.DataContext = _chat;
        Opened += (_, _) => _ = StartLocalShellAsync();
        Closed += (_, _) => _ = StopAsync();
    }

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
            StartAgent(connection);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            Status.Text = $"The shell couldn't start: {exception.Message}";
            Status.IsVisible = true;
        }
    }

    private void StartAgent(TerminalConnection connection)
    {
        var choice = ProviderFromEnvironment.Read(Environment.GetEnvironmentVariable);
        if (choice is null)
        {
            _chat.AddNotice(Strings.NoticeNoKey);
            return;
        }

        try
        {
            var redactor = new SecretRedactor();
            redactor.AddKnownSecret(choice.ApiKey ?? string.Empty);
            var client = ChatClientFactory.Create(choice.Settings, choice.ApiKey);
            _agent = new TerminalAgent(client, new ConnectionTerminal(connection), _chat, redactor);
            _chat.Attach(_agent, choice.PrivacyNotice);
        }
        catch (ArgumentException exception)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeProviderFailed, exception.Message));
        }
    }

    private async Task StopAsync()
    {
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
