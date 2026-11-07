namespace Datapoint.Terminal.Agent.Skills;

// A skill being rolled out across its hosts, batch by batch (product decision 103). The application keeps it, not the model:
// a host whose turn hasn't come can't be changed, and the next batch starts only once the last one is done and healthy.
// Changed is raised after every change, on whichever thread made it.
public sealed class SkillRun : IDisposable
{
    private readonly Lock _sync = new();
    private readonly List<RolloutHost> _hosts;
    private readonly int _width;
    private readonly TimeProvider _clock;
    private readonly CancellationTokenSource _stopped = new();
    private TaskCompletionSource _resumed = NewSignal();
    private int _batch;
    private bool _isPaused;
    private RolloutOutcome _outcome;

    public SkillRun(Skill skill, IReadOnlyList<string> hosts, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(hosts);
        Skill = skill;
        _clock = clock ?? TimeProvider.System;
        _hosts = [.. hosts.Select(host => new RolloutHost(host, RolloutHostState.Waiting))];
        _width = skill.Rollout.Width(hosts.Count);
        BatchCount = (hosts.Count + _width - 1) / _width;
    }

    public event EventHandler? Changed;

    public Guid Id { get; } = Guid.NewGuid();

    public Skill Skill { get; }

    public int BatchCount { get; }

    public CancellationToken StopToken => _stopped.Token;

    public bool IsOver
    {
        get
        {
            lock (_sync)
            {
                return _outcome != RolloutOutcome.Running;
            }
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (_sync)
            {
                return _isPaused;
            }
        }
    }

    public IReadOnlyList<string> Hosts
    {
        get
        {
            lock (_sync)
            {
                return [.. _hosts.Select(host => host.Name)];
            }
        }
    }

    public RolloutHost? Find(string name)
    {
        lock (_sync)
        {
            return _hosts.Find(host => string.Equals(host.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    // The batch being worked on, or nothing once the rollout is over.
    public IReadOnlyList<RolloutHost> Current
    {
        get
        {
            lock (_sync)
            {
                return _outcome == RolloutOutcome.Running ? [.. _hosts.Skip(_batch * _width).Take(_width)] : [];
            }
        }
    }

    // A host outside the rollout, or one it has reached, may be changed; one still waiting, or skipped, may not.
    public bool Allows(string? host) => host is null || Find(host) is not { State: RolloutHostState.Waiting or RolloutHostState.Skipped };

    public RolloutProgress Snapshot()
    {
        lock (_sync)
        {
            return new RolloutProgress(Id, Skill.Name, Skill.Title, Skill.Rollout, [.. _hosts])
            {
                Batch = Math.Min(_batch + 1, BatchCount),
                BatchCount = BatchCount,
                IsPaused = _isPaused,
                Outcome = _outcome,
            };
        }
    }

    // Starts the first batch, or the next one; the hosts it started, none once every batch is done.
    public IReadOnlyList<string> StartNext()
    {
        List<string> started;
        lock (_sync)
        {
            if (_outcome != RolloutOutcome.Running)
            {
                return [];
            }

            if (_hosts.Skip(_batch * _width).Take(_width).Any(host => host.State != RolloutHostState.Waiting))
            {
                _batch++;
            }

            if (_batch >= BatchCount)
            {
                _outcome = RolloutOutcome.Completed;
                started = [];
            }
            else
            {
                var now = _clock.GetUtcNow();
                started = [.. _hosts.Skip(_batch * _width).Take(_width).Select(host => host.Name)];
                Replace(started, host => host with { State = RolloutHostState.Working, StartedAt = now });
            }
        }

        Notify();
        return started;
    }

    public void Set(string name, RolloutHostState state, string? note = null, TimeSpan? healthyFor = null, bool failedCheck = false)
    {
        lock (_sync)
        {
            var isOver = state is RolloutHostState.Done or RolloutHostState.Failed or RolloutHostState.Skipped;
            var endedAt = isOver ? _clock.GetUtcNow() : (DateTimeOffset?)null;
            Replace([name], host => host with { State = state, Note = note, HealthyFor = healthyFor, FailedCheck = failedCheck, EndedAt = endedAt });
        }

        Notify();
    }

    // The batch carries on after the user pressed Pause, then waits here until they resume or stop.
    public void Pause(bool isPaused)
    {
        lock (_sync)
        {
            if (_isPaused == isPaused || _outcome != RolloutOutcome.Running)
            {
                return;
            }

            _isPaused = isPaused;
            if (isPaused)
            {
                _resumed = NewSignal();
            }
            else
            {
                _resumed.TrySetResult();
            }
        }

        Notify();
    }

    public Task WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        Task resumed;
        lock (_sync)
        {
            resumed = _isPaused ? _resumed.Task : Task.CompletedTask;
        }

        return resumed.WaitAsync(cancellationToken);
    }

    // Hosts not yet started are skipped; a host being worked on is left as it is, for the agent or the user to finish.
    public void Stop(RolloutOutcome outcome)
    {
        lock (_sync)
        {
            if (_outcome != RolloutOutcome.Running)
            {
                return;
            }

            _outcome = outcome;
            _isPaused = false;
            _resumed.TrySetResult();
            var waiting = _hosts.Where(host => host.State == RolloutHostState.Waiting).Select(host => host.Name).ToList();
            Replace(waiting, host => host with { State = RolloutHostState.Skipped });
        }

        _stopped.Cancel();
        Notify();
    }

    public void Dispose() => _stopped.Dispose();

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void Replace(IReadOnlyCollection<string> names, Func<RolloutHost, RolloutHost> change)
    {
        for (var index = 0; index < _hosts.Count; index++)
        {
            if (names.Contains(_hosts[index].Name, StringComparer.OrdinalIgnoreCase))
            {
                _hosts[index] = change(_hosts[index]);
            }
        }
    }

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}
