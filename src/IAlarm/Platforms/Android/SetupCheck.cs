using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AndroidSettings = Android.Provider.Settings;

namespace IAlarm.Services;

/// <summary>Phone settings that quietly stop alarms from ringing.</summary>
public enum SetupIssue
{
    Notifications,
    ExactAlarms,
    FullScreen,
    Battery,
}

public static class SetupCheck
{
    public static List<SetupIssue> Find()
    {
        var context = Platform.AppContext;
        var issues = new List<SetupIssue>();
        if (!NotificationManagerCompat.From(context)!.AreNotificationsEnabled())
            issues.Add(SetupIssue.Notifications);
        if (!AlarmScheduler.CanUseExactAlarms((AlarmManager)context.GetSystemService(Context.AlarmService)!))
            issues.Add(SetupIssue.ExactAlarms);
        if (OperatingSystem.IsAndroidVersionAtLeast(34)
            && !((NotificationManager)context.GetSystemService(Context.NotificationService)!).CanUseFullScreenIntent())
            issues.Add(SetupIssue.FullScreen);
        if (!((PowerManager)context.GetSystemService(Context.PowerService)!).IsIgnoringBatteryOptimizations(context.PackageName))
            issues.Add(SetupIssue.Battery);
        return issues;
    }

    public static string Describe(SetupIssue issue) => issue switch
    {
        SetupIssue.Notifications => "Notifications are off: the alarm screen can't appear.",
        SetupIssue.ExactAlarms => "Exact alarms are off: alarms may ring late.",
        SetupIssue.FullScreen => "Full-screen alerts are off: the alarm won't show over the lock screen.",
        SetupIssue.Battery => "Battery optimization may stop alarms on some phones. Allow iAlarm to run in the background.",
        _ => "",
    };

    /// <summary>Asks in-app where Android allows it, otherwise opens the right system settings screen.</summary>
    public static async Task FixAsync(SetupIssue issue)
    {
        var package = Platform.AppContext.PackageName!;
        switch (issue)
        {
            case SetupIssue.Notifications:
                if (OperatingSystem.IsAndroidVersionAtLeast(33)
                    && await Permissions.RequestAsync<Permissions.PostNotifications>() == PermissionStatus.Granted)
                    return;
                Open(new Intent(AndroidSettings.ActionAppNotificationSettings).PutExtra(AndroidSettings.ExtraAppPackage, package)!);
                break;
            case SetupIssue.ExactAlarms:
                if (OperatingSystem.IsAndroidVersionAtLeast(31))
                    Open(new Intent(AndroidSettings.ActionRequestScheduleExactAlarm, Android.Net.Uri.Parse("package:" + package)));
                break;
            case SetupIssue.FullScreen:
                if (OperatingSystem.IsAndroidVersionAtLeast(34))
                    Open(new Intent(AndroidSettings.ActionManageAppUseFullScreenIntent, Android.Net.Uri.Parse("package:" + package)));
                break;
            case SetupIssue.Battery:
                Open(new Intent(AndroidSettings.ActionRequestIgnoreBatteryOptimizations, Android.Net.Uri.Parse("package:" + package)));
                break;
        }
    }

    /// <summary>On the first alarm: the notification prompt, which Android shows in the app.</summary>
    public static async Task AskForNotificationsAsync()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && Find().Contains(SetupIssue.Notifications))
            await Permissions.RequestAsync<Permissions.PostNotifications>();
    }

    static void Open(Intent intent)
    {
        try
        {
            if (Platform.CurrentActivity is { } activity)
                activity.StartActivity(intent);
            else
                Platform.AppContext.StartActivity(intent.AddFlags(ActivityFlags.NewTask));
        }
        catch (ActivityNotFoundException)
        {
            // Some phones lack this screen; fall back to the app's own settings page.
            var details = new Intent(AndroidSettings.ActionApplicationDetailsSettings,
                Android.Net.Uri.Parse("package:" + Platform.AppContext.PackageName)).AddFlags(ActivityFlags.NewTask);
            Platform.AppContext.StartActivity(details);
        }
    }
}
