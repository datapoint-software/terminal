namespace Datapoint.Terminal.Agent;

/// <summary>Wakes a waiting task whenever the terminal shows new output or ends.</summary>
internal sealed class OutputSignal : IDisposable
{
    private readonly IAgentTerminal _terminal;
    private readonly SemaphoreSlim _signal = new(0, 1);

    public OutputSignal(IAgentTerminal terminal)
    {
        _terminal = terminal;
        _terminal.OutputArrived += OnOutput;
        _terminal.Ended += OnEnded;
    }

    public DateTimeOffset LastOutputAt { get; private set; } = DateTimeOffset.UtcNow;

    public bool HasEnded { get; private set; }

    /// <summary>Waits for new output, at most <paramref name="timeout"/>; returns whether any arrived.</summary>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _signal.WaitAsync(timeout <= TimeSpan.Zero ? TimeSpan.Zero : timeout, cancellationToken);

    public void Dispose()
    {
        _terminal.OutputArrived -= OnOutput;
        _terminal.Ended -= OnEnded;
        _signal.Dispose();
    }

    private void OnOutput(object? sender, EventArgs e)
    {
        LastOutputAt = DateTimeOffset.UtcNow;
        Release();
    }

    private void OnEnded(object? sender, EventArgs e)
    {
        HasEnded = true;
        Release();
    }

    private void Release()
    {
        try
        {
            if (_signal.CurrentCount == 0)
            {
                _signal.Release();
            }
        }
        catch (Exception exception) when (exception is SemaphoreFullException or ObjectDisposedException)
        {
            // Already signalled, or the wait is over.
        }
    }
}
