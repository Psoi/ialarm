namespace IAlarm.Core;

/// <summary>
/// A stopwatch kept as timestamps, not as a ticking counter, so it stays right while the app is closed,
/// the screen is off or the screen rotates. Times are milliseconds on a monotonic clock.
/// </summary>
public readonly record struct StopwatchState(bool Running, long AccumulatedMs, long StartedAtMs)
{
    public long Elapsed(long nowMs) => Running ? AccumulatedMs + Math.Max(0, nowMs - StartedAtMs) : AccumulatedMs;

    public StopwatchState Start(long nowMs) => Running ? this : this with { Running = true, StartedAtMs = nowMs };

    public StopwatchState Stop(long nowMs) => Running ? new StopwatchState(false, Elapsed(nowMs), 0) : this;

    public static StopwatchState Reset => default;

    /// <summary>The monotonic clock restarts at boot; a running stopwatch from before it can't continue.</summary>
    public StopwatchState AfterClockReset(long nowMs) =>
        Running && StartedAtMs > nowMs ? new StopwatchState(false, AccumulatedMs, 0) : this;
}
