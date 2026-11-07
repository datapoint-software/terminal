namespace Datapoint.Terminal.Sessions.Ssh;

// One connection used by several terminals, e.g. the panes of a split, so the user logs in once: it closes with the last of them.
public sealed class SharedSshClient(SshClient client)
{
    private readonly Lock _lock = new();
    private int _users;

    public SshClient Client { get; } = client;

    public bool IsClosed { get; private set; }

    // Null once the connection has closed; disposing the result gives the use back.
    public IAsyncDisposable? TryUse()
    {
        lock (_lock)
        {
            if (IsClosed)
            {
                return null;
            }

            _users++;
            return new Use(this);
        }
    }

    private async ValueTask ReleaseAsync()
    {
        lock (_lock)
        {
            if (--_users > 0)
            {
                return;
            }

            IsClosed = true;
        }

        await Client.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class Use(SharedSshClient owner) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync() => Interlocked.Exchange(ref _released, 1) == 0 ? owner.ReleaseAsync() : ValueTask.CompletedTask;
    }
}
