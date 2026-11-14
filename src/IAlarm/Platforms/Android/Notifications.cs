using Android.App;
using Android.Content;
using AndroidX.Core.App;
using IAlarm.Core;

namespace IAlarm.Services;

static class Notifications
{
    public const string AlarmChannel = "alarm";
    public const string MissedChannel = "missed";
    public const string StopwatchChannel = "stopwatch";

    public const int RingingId = 1001;
    public const int StopwatchId = 1002;

    public static void EnsureChannels(Context context)
    {
        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;

        // Silent: the ringing service plays the sound on the alarm stream and vibrates itself.
        var alarm = new NotificationChannel(AlarmChannel, "Ringing alarms", NotificationImportance.High)
        {
            LockscreenVisibility = NotificationVisibility.Public,
        };
        alarm.SetSound(null, null);
        alarm.EnableVibration(false);
        manager.CreateNotificationChannel(alarm);

        manager.CreateNotificationChannel(new NotificationChannel(MissedChannel, "Missed alarms", NotificationImportance.Default));

        var stopwatch = new NotificationChannel(StopwatchChannel, "Stopwatch", NotificationImportance.Low);
        stopwatch.SetShowBadge(false);
        manager.CreateNotificationChannel(stopwatch);
    }

    public static void Missed(Context context, Alarm alarm, string why)
    {
        var notification = new NotificationCompat.Builder(context, MissedChannel)
            .SetSmallIcon(Resource.Drawable.ic_notification)!
            .SetContentTitle($"{TimeText.Clock(alarm.At)} {alarm.Label ?? "Alarm"}")!
            .SetContentText(why)!
            .SetContentIntent(OpenApp(context, "alarms", 10))!
            .SetAutoCancel(true)!
            .Build()!;
        Post(context, 2000 + (alarm.Id.GetHashCode() & 0xFFFF), notification);
    }

    public static PendingIntent OpenApp(Context context, string tab, int requestCode)
    {
        var launch = new Intent(context, typeof(MainActivity))
            .PutExtra(MainActivity.TabExtra, tab)!
            .AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop)!;
        return PendingIntent.GetActivity(context, requestCode, launch, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }

    public static void Post(Context context, int id, Notification notification)
    {
        try
        {
            NotificationManagerCompat.From(context)!.Notify(id, notification);
        }
        catch (Java.Lang.SecurityException)
        {
            // Notification permission not granted; the Alarm tab shows how to fix it.
        }
    }

    public static void Cancel(Context context, int id) => NotificationManagerCompat.From(context)!.Cancel(id);
}
