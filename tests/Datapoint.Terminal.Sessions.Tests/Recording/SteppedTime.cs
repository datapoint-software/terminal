namespace Datapoint.Terminal.Sessions.Tests.Recording;

internal sealed class SteppedTime : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero).AddTicks(_ticks);

    public void Advance(double seconds) => _ticks += TimeSpan.FromSeconds(seconds).Ticks;
}
