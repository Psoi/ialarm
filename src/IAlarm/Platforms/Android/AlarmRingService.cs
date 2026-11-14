using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using AndroidX.Core.App;
using IAlarm.Core;

namespace IAlarm.Services;

/// <summary>
/// Rings: plays the alarm sound on the alarm stream (so silent mode doesn't mute it), vibrates, and shows the
/// full-screen <see cref="AlarmActivity"/> over the lock screen. A foreground service keeps it going while the
/// phone is locked. It stops on Dismiss, Snooze, or after <see cref="RingFor"/>.
/// </summary>
[Service(Exported = false, DirectBootAware = true, ForegroundServiceType = ForegroundService.TypeSystemExempted)]
public sealed class AlarmRingService : Service
{
    const string IdsExtra = "ids";
    static readonly TimeSpan RingFor = TimeSpan.FromMinutes(10);

    static readonly Lock gate = new();
    static readonly List<string> ringing = [];
    static AlarmRingService? current;

    MediaPlayer? player;
    ToneGenerator? tone;
    Vibrator? vibrator;
    PowerManager.WakeLock? wakeLock;
    Handler? handler;
    Java.Lang.Runnable? beep;
    Java.Lang.Runnable? timeout;

    /// <summary>Raised on the main thread when alarms start or stop ringing.</summary>
    public static event Action? RingingChanged;

    /// <summary>Ids of the alarms ringing now.</summary>
    public static IReadOnlyList<string> Ringing
    {
        get
        {
            lock (gate)
                return [.. ringing];
        }
    }

    public static void Ring(Context context, IEnumerable<string> ids)
    {
        var intent = new Intent(context, typeof(AlarmRingService)).PutExtra(IdsExtra, ids.ToArray())!;
        context.StartForegroundService(intent);
    }

    /// <summary>Main thread only, like the buttons and notification actions that call it.</summary>
    public static void Dismiss() => current?.Finish(snooze: false);

    public static void Snooze() => current?.Finish(snooze: true);

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        current = this;
        handler = new Handler(Looper.MainLooper!);
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var ids = intent?.GetStringArrayExtra(IdsExtra) ?? [];
        lock (gate)
            foreach (var id in ids)
                if (!ringing.Contains(id))
                    ringing.Add(id);

        GoForeground();
        if (Ringing.Count == 0)
        {
            // Restarted without anything to ring.
            Finish(snooze: false);
            return StartCommandResult.NotSticky;
        }

        StartAlerting();
        ShowScreen();
        RestartTimeout();
        RingingChanged?.Invoke();
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        StopAlerting();
        if (current == this)
            current = null;
        base.OnDestroy();
    }

    /// <summary>"07:30 Wake up" for each ringing alarm.</summary>
    public static List<string> Titles(Context context)
    {
        var repo = AlarmRepository.Get(context);
        return Ringing.Select(repo.Find).OfType<Alarm>()
            .Select(a => $"{TimeText.Clock(a.At)}  {a.Label ?? "ALARM"}")
            .ToList();
    }

    void GoForeground()
    {
        Notifications.EnsureChannels(this);
        var titles = Titles(this);
        var screen = PendingIntent.GetActivity(this, 0,
            new Intent(this, typeof(AlarmActivity)).AddFlags(ActivityFlags.NewTask | ActivityFlags.NoUserAction)!,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

        var notification = new NotificationCompat.Builder(this, Notifications.AlarmChannel)
            .SetSmallIcon(Resource.Drawable.ic_notification)!
            .SetContentTitle(titles.FirstOrDefault() ?? "Alarm")!
            .SetContentText(titles.Count > 1 ? string.Join(", ", titles.Skip(1)) : "Ringing")!
            .SetCategory(NotificationCompat.CategoryAlarm)!
            .SetPriority(NotificationCompat.PriorityMax)!
            .SetVisibility(NotificationCompat.VisibilityPublic)!
            .SetOngoing(true)!
            .SetContentIntent(screen)!
            .SetFullScreenIntent(screen, true)!
            .AddAction(0, $"Snooze {AlarmBook.SnoozeMinutes} min", RingActionReceiver.Intent(this, RingActionReceiver.SnoozeAction))!
            .AddAction(0, "Dismiss", RingActionReceiver.Intent(this, RingActionReceiver.DismissAction))!
            .Build()!;

        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(34))
                StartForeground(Notifications.RingingId, notification, ForegroundService.TypeSystemExempted);
            else
                StartForeground(Notifications.RingingId, notification);
        }
        catch (Java.Lang.Exception)
        {
            // Not allowed as a foreground service: still post the notification and ring for as long as we live.
            Notifications.Post(this, Notifications.RingingId, notification);
        }
    }

    void ShowScreen()
    {
        // Shown directly when allowed; otherwise the full-screen notification opens it.
        try
        {
            StartActivity(new Intent(this, typeof(AlarmActivity)).AddFlags(ActivityFlags.NewTask | ActivityFlags.NoUserAction));
        }
        catch (Java.Lang.Exception)
        {
        }
    }

    static AudioAttributes AlarmAudio => new AudioAttributes.Builder()
        .SetUsage(AudioUsageKind.Alarm)!
        .SetContentType(AudioContentType.Sonification)!
        .Build()!;

    void StartAlerting()
    {
        if (player is not null || tone is not null)
            return;

        var power = (PowerManager)GetSystemService(PowerService)!;
        wakeLock = power.NewWakeLock(WakeLockFlags.Partial, "iAlarm:ring");
        wakeLock!.Acquire((long)(RingFor + TimeSpan.FromMinutes(1)).TotalMilliseconds);

        EnsureAudible();
        if (!TryPlay(RingtoneManager.GetActualDefaultRingtoneUri(this, RingtoneType.Alarm))
            && !TryPlay(RingtoneManager.GetDefaultUri(RingtoneType.Alarm))
            && !TryPlay(RingtoneManager.GetDefaultUri(RingtoneType.Ringtone)))
            StartBeeping();

        Vibrate();
    }

    bool TryPlay(Android.Net.Uri? uri)
    {
        if (uri is null)
            return false;
        var p = new MediaPlayer();
        try
        {
            p.SetAudioAttributes(AlarmAudio);
            p.SetDataSource(this, uri);
            p.Looping = true;
            p.Prepare();
            p.Start();
            player = p;
            return true;
        }
        catch (Exception)
        {
            // No such sound, or not readable yet (before the first unlock after a restart).
            p.Release();
            return false;
        }
    }

    /// <summary>The fallback: a digital-watch double beep every second.</summary>
    void StartBeeping()
    {
        try
        {
            tone = new ToneGenerator(Android.Media.Stream.Alarm, 100);
        }
        catch (Java.Lang.RuntimeException)
        {
            return;
        }
        beep = new Java.Lang.Runnable(() =>
        {
            tone?.StartTone(Tone.PropBeep2, 600);
            handler?.PostDelayed(beep!, 1000);
        });
        handler!.Post(beep);
    }

    /// <summary>If the alarm volume is all the way down, an alarm would ring silently: raise it to two thirds.</summary>
    void EnsureAudible()
    {
        var audio = (AudioManager)GetSystemService(AudioService)!;
        if (audio.GetStreamVolume(Android.Media.Stream.Alarm) > 0)
            return;
        try
        {
            audio.SetStreamVolume(Android.Media.Stream.Alarm, Math.Max(1, audio.GetStreamMaxVolume(Android.Media.Stream.Alarm) * 2 / 3), 0);
        }
        catch (Java.Lang.SecurityException)
        {
        }
    }

#pragma warning disable CA1422 // Vibrator(Service) and Vibrate(effect, attributes) still work on newer Android
    void Vibrate()
    {
        vibrator = OperatingSystem.IsAndroidVersionAtLeast(31)
            ? ((VibratorManager)GetSystemService(VibratorManagerService)!).DefaultVibrator
            : (Vibrator?)GetSystemService(VibratorService);
        if (vibrator?.HasVibrator != true)
            return;
        vibrator.Vibrate(VibrationEffect.CreateWaveform([0, 800, 800], 0), AlarmAudio);
    }
#pragma warning restore CA1422

    void StopAlerting()
    {
        if (player is not null)
        {
            try
            {
                player.Stop();
            }
            catch (Java.Lang.IllegalStateException)
            {
            }
            player.Release();
            player = null;
        }
        if (beep is not null)
            handler?.RemoveCallbacks(beep);
        beep = null;
        tone?.Release();
        tone = null;
        vibrator?.Cancel();
        vibrator = null;
        if (timeout is not null)
            handler?.RemoveCallbacks(timeout);
        timeout = null;
        if (wakeLock?.IsHeld == true)
            wakeLock.Release();
        wakeLock = null;
    }

    void RestartTimeout()
    {
        if (timeout is not null)
            handler!.RemoveCallbacks(timeout);
        timeout = new Java.Lang.Runnable(() =>
        {
            var repo = AlarmRepository.Get(this);
            foreach (var alarm in Ringing.Select(repo.Find).OfType<Alarm>())
                Notifications.Missed(this, alarm, $"Rang for {RingFor.TotalMinutes:0} minutes with no answer");
            Finish(snooze: false);
        });
        handler!.PostDelayed(timeout, (long)RingFor.TotalMilliseconds);
    }

    void Finish(bool snooze)
    {
        string[] ids;
        lock (gate)
        {
            ids = [.. ringing];
            ringing.Clear();
        }
        if (snooze && ids.Length > 0)
        {
            var now = DateTime.Now;
            AlarmRepository.Get(this).Change(list =>
            {
                AlarmBook.Snooze(list, ids, now);
                return 0;
            });
        }

        StopAlerting();
        StopForeground(StopForegroundFlags.Remove);
        Notifications.Cancel(this, Notifications.RingingId);
        StopSelf();
        RingingChanged?.Invoke();
    }
}

/// <summary>Snooze and Dismiss from the ringing notification.</summary>
[BroadcastReceiver(Enabled = true, Exported = false, DirectBootAware = true)]
public class RingActionReceiver : BroadcastReceiver
{
    public const string SnoozeAction = "com.personal.ialarm.SNOOZE";
    public const string DismissAction = "com.personal.ialarm.DISMISS";

    public static PendingIntent Intent(Context context, string action) =>
        PendingIntent.GetBroadcast(context, action == SnoozeAction ? 1 : 2,
            new Intent(context, typeof(RingActionReceiver)).SetAction(action),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == SnoozeAction)
            AlarmRingService.Snooze();
        else
            AlarmRingService.Dismiss();
    }
}
