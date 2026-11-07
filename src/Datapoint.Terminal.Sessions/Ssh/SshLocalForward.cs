using System.Net;
using System.Net.Sockets;
using Datapoint.Terminal.Sessions.Ssh.Channels;

namespace Datapoint.Terminal.Sessions.Ssh;

// Listens on this computer's loopback address only, never on the network, and carries each connection to host:port as seen
// from the server, over its own channel of the connection.
public sealed class SshLocalForward : IAsyncDisposable
{
    private readonly SshClient _client;
    private readonly TcpListener _listener;
    private readonly string _host;
    private readonly int _port;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _accepting;

    private SshLocalForward(SshClient client, TcpListener listener, string host, int port)
    {
        _client = client;
        _listener = listener;
        _host = host;
        _port = port;
        LocalPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        _accepting = Task.Run(() => AcceptAsync(_stop.Token), CancellationToken.None);
    }

    public int LocalPort { get; }

    // Port 0 picks a free port, reported in LocalPort.
    public static SshLocalForward Start(SshClient client, int localPort, string host, int port)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        var listener = new TcpListener(IPAddress.Loopback, localPort);
        listener.Start();
        return new SshLocalForward(client, listener, host, port);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003", Justification = "Started on the thread pool in the constructor.")]
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        try
        {
            await _accepting.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }

        _listener.Dispose();
        _stop.Dispose();
    }

    private async Task AcceptAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket socket;
            try
            {
                socket = await _listener.AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => CarryAsync(socket, cancellationToken), CancellationToken.None);
        }
    }

    // A connection the server can't make is closed straight away, as OpenSSH does.
    private async Task CarryAsync(Socket socket, CancellationToken cancellationToken)
    {
        using (socket)
        {
            SshChannel channel;
            try
            {
                channel = await _client.OpenDirectTcpIpAsync(_host, _port, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsClosed(exception))
            {
                return;
            }

            var stream = new SshChannelStream(channel);
            await using (stream.ConfigureAwait(false))
            {
                using var network = new NetworkStream(socket, ownsSocket: false);
                try
                {
                    await Task.WhenAll(UpAsync(network, stream, channel, cancellationToken), DownAsync(stream, socket, network, cancellationToken)).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsClosed(exception))
                {
                    // Either side went away.
                }
            }
        }
    }

    private static async Task UpAsync(NetworkStream network, SshChannelStream stream, SshChannel channel, CancellationToken cancellationToken)
    {
        await network.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        await channel.SendEofAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DownAsync(SshChannelStream stream, Socket socket, NetworkStream network, CancellationToken cancellationToken)
    {
        await stream.CopyToAsync(network, cancellationToken).ConfigureAwait(false);
        socket.Shutdown(SocketShutdown.Send);
    }

    private static bool IsClosed(Exception exception) =>
        exception is SshChannelException or SshDisconnectedException or IOException or SocketException or ObjectDisposedException or OperationCanceledException;
}
