namespace Datapoint.Terminal.Tests.Shell;

// A clock that moves only when a test advances it, so timers fire at the same point in a test however slow the machine is.
internal sealed class ManualTime : TimeProvider
{
    private readonly List<Timer> _timers = [];
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public override long GetTimestamp() => _now.UtcTicks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Timer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        _now += by;
        List<Timer> due;
        lock (_timers)
        {
            due = _timers.Where(timer => timer.Due <= _now).ToList();
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    private sealed class Timer(ManualTime time, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (time._timers)
            {
                time._timers.Remove(this);
                _period = period;
                if (dueTime == Timeout.InfiniteTimeSpan)
                {
                    return true;
                }

                Due = time._now + dueTime;
                time._timers.Add(this);
            }

            return true;
        }

        public void Fire()
        {
            Change(_period, _period);
            callback(state);
        }

        public void Dispose()
        {
            lock (time._timers)
            {
                time._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
