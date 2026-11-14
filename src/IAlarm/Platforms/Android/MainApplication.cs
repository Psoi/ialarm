using Android.App;
using Android.Runtime;
using IAlarm.Services;

namespace IAlarm;

/// <summary>Direct-boot aware, so alarms can ring after a restart before the phone is first unlocked.</summary>
[Application(DirectBootAware = true)]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();
        Notifications.EnsureChannels(this);
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
