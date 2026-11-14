using Android.App;
using Android.Content;
using IAlarm.Core;

namespace IAlarm.Services;

/// <summary>
/// Alarms as a chain of one system alarm, always set for the earliest alarm that is on. It uses
/// <see cref="AlarmManager.SetAlarmClock"/>: exact, allowed in Doze, and shown as the alarm icon in the status bar.
/// </summary>
static class AlarmScheduler
{
    public static void Arm(Context context, IReadOnlyList<Alarm> alarms)
    {
        var manager = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var fire = PendingIntent.GetBroadcast(context, 0, new Intent(context, typeof(AlarmReceiver)),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

        if (AlarmBook.NextTrigger(alarms) is not { } next)
        {
            manager.Cancel(fire);
            return;
        }

        // A trigger already past is due now (the phone was off, or the clock moved).
        var at = Math.Max(ToUnixMs(next), Java.Lang.JavaSystem.CurrentTimeMillis() + 1000);
        if (CanUseExactAlarms(manager))
        {
            var show = PendingIntent.GetActivity(context, 1,
                new Intent(context, typeof(MainActivity)).PutExtra(MainActivity.TabExtra, "alarms")!,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
            manager.SetAlarmClock(new AlarmManager.AlarmClockInfo(at, show), fire);
        }
        else
        {
            // Without exact-alarm access Android may deliver this some minutes late.
            manager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, at, fire);
        }
    }

    public static bool CanUseExactAlarms(AlarmManager manager) =>
        !OperatingSystem.IsAndroidVersionAtLeast(31) || manager.CanScheduleExactAlarms();

    static long ToUnixMs(DateTime local) =>
        new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local)).ToUnixTimeMilliseconds();

    /// <summary>The system alarm went off: ring what is due.</summary>
    public static void Fire(Context context)
    {
        var now = DateTime.Now;
        var due = AlarmRepository.Get(context).Change(list => AlarmBook.TakeDue(list, now));
        if (due.Count == 0)
            return;
        try
        {
            AlarmRingService.Ring(context, due.Select(a => a.Id));
        }
        catch (Java.Lang.Exception)
        {
            // Android refused to start the ringing service from the background (only possible without exact-alarm
            // access). At least tell the user.
            foreach (var alarm in due)
                Notifications.Missed(context, alarm, "Couldn't ring. Open iAlarm and fix the warnings on the Alarm tab.");
        }
    }

    /// <summary>After a restart, update or clock change: drop long-missed alarms (with a notice) and re-arm.</summary>
    public static void Recover(Context context)
    {
        TimeZoneInfo.ClearCachedData();
        var now = DateTime.Now;
        var missed = AlarmRepository.Get(context).Change(list => AlarmBook.ExpireMissed(list, now));
        foreach (var alarm in missed)
            Notifications.Missed(context, alarm, "Missed while the phone was off");
    }
}

[BroadcastReceiver(Enabled = true, Exported = false, DirectBootAware = true)]
public class AlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null)
            AlarmScheduler.Fire(context);
    }
}

/// <summary>System alarms are lost on restart and app updates, and need recomputing when the clock, zone or exact-alarm permission changes.</summary>
[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = true)]
[IntentFilter([Intent.ActionLockedBootCompleted, Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced,
    Intent.ActionTimeChanged, Intent.ActionTimezoneChanged, "android.app.action.SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED"])]
public class RescheduleReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is not null)
            AlarmScheduler.Recover(context);
    }
}
