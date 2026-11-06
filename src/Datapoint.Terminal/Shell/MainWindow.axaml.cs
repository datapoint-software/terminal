using Avalonia.Controls;
using Avalonia.Threading;
using Datapoint.Terminal.Controls;
using Datapoint.Terminal.Sessions;
using Datapoint.Terminal.Sessions.Local;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow : Window
{
    private TerminalConnection? _connection;

    public MainWindow()
    {
        InitializeComponent();
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
            Terminal.Input += (_, e) => connection.Send(e.Data);
            Terminal.Resized += (_, e) => _ = connection.ResizeAsync(e.Columns, e.Rows, CancellationToken.None);
            Terminal.Terminal = connection.Terminal;
            Terminal.Focus();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            Status.Text = $"The shell couldn't start: {exception.Message}";
            Status.IsVisible = true;
        }
    }

    private async Task StopAsync()
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    private static string TitleFor(string shellTitle) =>
        string.IsNullOrWhiteSpace(shellTitle) ? "Datapoint Terminal" : $"{shellTitle} — Datapoint Terminal";
}
