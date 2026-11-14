using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using IAlarm.Core;

namespace IAlarm.Services;

/// <summary>
/// The stopwatch, kept in preferences as timestamps on the monotonic clock (which also counts deep sleep).
/// Nothing has to keep running: the screen computes the time from the timestamps, and the ongoing notification
/// uses Android's own chronometer, so it keeps counting even if the app is closed.
/// </summary>
public static class StopwatchStore
{
    const string Prefs = "stopwatch";
    static readonly Lock gate = new();

    /// <summary>Raised after Start, Stop or Reset, from the screen or the notification.</summary>
    public static event Action? Changed;

    public static long Now => SystemClock.ElapsedRealtime();

    public static StopwatchState Load(Context context)
    {
        var prefs = context.GetSharedPreferences(Prefs, FileCreationMode.Private)!;
        return new StopwatchState(prefs.GetBoolean("running", false), prefs.GetLong("accumulated", 0), prefs.GetLong("started", 0))
            .AfterClockReset(Now);
    }

    public static StopwatchState Start(Context context) => Apply(context, (s, now) => s.Start(now));

    public static StopwatchState Stop(Context context) => Apply(context, (s, now) => s.Stop(now));

    public static StopwatchState Reset(Context context) => Apply(context, (_, _) => StopwatchState.Reset);

    static StopwatchState Apply(Context context, Func<StopwatchState, long, StopwatchState> change)
    {
        StopwatchState state;
        lock (gate)
        {
            state = change(Load(context), Now);
            context.GetSharedPreferences(Prefs, FileCreationMode.Private)!.Edit()!
                .PutBoolean("running", state.Running)!
                .PutLong("accumulated", state.AccumulatedMs)!
                .PutLong("started", state.StartedAtMs)!
                .Apply();
        }
        ShowNotification(context, state);
        Changed?.Invoke();
        return state;
    }

    /// <summary>Running: an ongoing, ticking notification. Paused: a quiet one to resume from. Reset: none.</summary>
    static void ShowNotification(Context context, StopwatchState state)
    {
        var elapsed = state.Elapsed(Now);
        if (!state.Running && elapsed == 0)
        {
            Notifications.Cancel(context, Notifications.StopwatchId);
            return;
        }

        var builder = new NotificationCompat.Builder(context, Notifications.StopwatchChannel)
            .SetSmallIcon(Resource.Drawable.ic_notification)!
            .SetContentIntent(Notifications.OpenApp(context, "stopwatch", 20))!
            .SetCategory(NotificationCompat.CategoryStopwatch)!
            .SetVisibility(NotificationCompat.VisibilityPublic)!
            .SetSilent(true)!
            .SetOnlyAlertOnce(true)!;

        if (state.Running)
        {
            builder.SetContentTitle("Stopwatch")!
                .SetUsesChronometer(true)!
                .SetShowWhen(true)!
                .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis() - elapsed)!
                .SetOngoing(true)!
                .AddAction(0, "Stop", StopwatchActionReceiver.Intent(context, StopwatchActionReceiver.StopAction))!
                .AddAction(0, "Reset", StopwatchActionReceiver.Intent(context, StopwatchActionReceiver.ResetAction));
        }
        else
        {
            builder.SetContentTitle($"Stopwatch paused at {TimeText.Stopwatch(elapsed)}")!
                .SetShowWhen(false)!
                .SetOngoing(false)!
                .AddAction(0, "Start", StopwatchActionReceiver.Intent(context, StopwatchActionReceiver.StartAction))!
                .AddAction(0, "Reset", StopwatchActionReceiver.Intent(context, StopwatchActionReceiver.ResetAction));
        }
        Notifications.Post(context, Notifications.StopwatchId, builder.Build()!);
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public class StopwatchActionReceiver : BroadcastReceiver
{
    public const string StartAction = "com.personal.ialarm.STOPWATCH_START";
    public const string StopAction = "com.personal.ialarm.STOPWATCH_STOP";
    public const string ResetAction = "com.personal.ialarm.STOPWATCH_RESET";

    public static PendingIntent Intent(Context context, string action) =>
        PendingIntent.GetBroadcast(context, action.GetHashCode(StringComparison.Ordinal) & 0xFFFF,
            new Intent(context, typeof(StopwatchActionReceiver)).SetAction(action),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
            return;
        switch (intent?.Action)
        {
            case StartAction: StopwatchStore.Start(context); break;
            case StopAction: StopwatchStore.Stop(context); break;
            case ResetAction: StopwatchStore.Reset(context); break;
        }
    }
}
