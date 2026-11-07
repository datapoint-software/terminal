namespace Datapoint.Terminal.Agent.Tests.Fakes;

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => now;
}
