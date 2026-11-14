using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace IAlarm;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    /// <summary>Notifications open a tab: "alarms" or "stopwatch".</summary>
    public const string TabExtra = "tab";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (savedInstanceState is null)
            TakeTab(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        TakeTab(intent);
    }

    static void TakeTab(Intent? intent)
    {
        if (intent?.GetStringExtra(TabExtra) is { Length: > 0 } tab)
            App.OpenTab(tab);
    }
}
