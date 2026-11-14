using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using IAlarm.Core;
using IAlarm.Services;
using IAlarm.Views;
using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Platform;
using Color = Microsoft.Maui.Graphics.Color;
using View = Android.Views.View;

namespace IAlarm;

/// <summary>
/// The full-screen ringing screen, shown over the lock screen: the time in big segments, the alarm labels,
/// and Snooze / Dismiss. A plain Android activity (not MAUI) so it opens fast, even from a locked phone.
/// </summary>
[Activity(Theme = "@style/AlarmTheme", Exported = false, DirectBootAware = true, LaunchMode = LaunchMode.SingleTask,
    ExcludeFromRecents = true, TaskAffinity = "com.personal.ialarm.ringing", ShowWhenLocked = true, TurnScreenOn = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.KeyboardHidden)]
public sealed class AlarmActivity : Activity
{
    readonly SegmentDrawable clock = new();
    PlatformGraphicsView? clockView;
    TextView? date;
    TextView? labels;
    Handler? handler;
    Java.Lang.Runnable? tick;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ShowOverLockScreen();
        if (AlarmRingService.Ringing.Count == 0)
        {
            Finish();
            return;
        }

        var dark = (Resources!.Configuration!.UiMode & UiMode.NightMask) == UiMode.NightYes;
        SetContentView(Build(dark ? Palette.Dark : Palette.Light));
        AlarmRingService.RingingChanged += OnRingingChanged;
        handler = new Handler(Looper.MainLooper!);
        tick = new Java.Lang.Runnable(Tick);
        Tick();
    }

    protected override void OnDestroy()
    {
        AlarmRingService.RingingChanged -= OnRingingChanged;
        if (tick is not null)
            handler?.RemoveCallbacks(tick);
        base.OnDestroy();
    }

#pragma warning disable CA1422, CS0618, CS0672 // The ringing screen must be answered with a button, not Back.
    public override void OnBackPressed()
    {
    }
#pragma warning restore CA1422, CS0618, CS0672

    void ShowOverLockScreen()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(27))
        {
            SetShowWhenLocked(true);
            SetTurnScreenOn(true);
        }
        else
        {
#pragma warning disable CA1422
            Window!.AddFlags(WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.TurnScreenOn);
#pragma warning restore CA1422
        }
        Window!.AddFlags(WindowManagerFlags.KeepScreenOn);
    }

    void OnRingingChanged() => RunOnUiThread(() =>
    {
        if (AlarmRingService.Ringing.Count == 0)
            Finish();
        else
            labels!.Text = string.Join("\n", AlarmRingService.Titles(this));
    });

    void Tick()
    {
        var now = DateTime.Now;
        clock.Text = TimeText.Clock(now, colonOn: now.Millisecond < 500);
        clockView?.Invalidate();
        date!.Text = TimeText.Date(now);
        handler?.PostDelayed(tick!, 500 - now.Millisecond % 500);
    }

    View Build(Palette palette)
    {
        clock.LitColor = palette.Lit;
        clock.GhostColor = palette.Ghost;
        clock.GlowColor = palette.Glow;

        var root = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
        root.SetGravity(GravityFlags.Center);
        root.SetPadding(Dp(24), Dp(24), Dp(24), Dp(32));
        root.SetBackgroundColor(palette.Background.ToPlatform());

        root.AddView(Text("ALARM", 16, palette.Muted, bold: true), Wrap());

        var panel = new FrameLayout(this);
        panel.Background = Box(palette.Panel, palette.Muted, 1);
        panel.SetPadding(Dp(12), Dp(20), Dp(12), Dp(20));
        clockView = new PlatformGraphicsView(this, clock);
        panel.AddView(clockView, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(140)));
        var panelParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        panelParams.SetMargins(0, Dp(16), 0, Dp(16));
        root.AddView(panel, panelParams);

        date = Text("", 16, palette.Muted, bold: false);
        root.AddView(date, Wrap());

        labels = Text(string.Join("\n", AlarmRingService.Titles(this)), 24, palette.Text, bold: true);
        var labelParams = Wrap();
        labelParams.SetMargins(0, Dp(24), 0, Dp(40));
        root.AddView(labels, labelParams);

        var buttons = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal };
        buttons.AddView(Button($"SNOOZE {AlarmBook.SnoozeMinutes}", palette, filled: false, AlarmRingService.Snooze), Weighted(end: Dp(8)));
        buttons.AddView(Button("DISMISS", palette, filled: true, AlarmRingService.Dismiss), Weighted(start: Dp(8)));
        root.AddView(buttons, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(72)));
        return root;
    }

    TextView Text(string text, float size, Color color, bool bold)
    {
        var view = new TextView(this) { Text = text, Gravity = GravityFlags.Center, LetterSpacing = 0.12f };
        view.SetTextSize(ComplexUnitType.Sp, size);
        view.SetTextColor(color.ToPlatform());
        view.SetTypeface(Typeface.Monospace, bold ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        return view;
    }

    Android.Widget.Button Button(string text, Palette palette, bool filled, Action click)
    {
        var button = new Android.Widget.Button(this) { Text = text, LetterSpacing = 0.12f };
        button.SetTextSize(ComplexUnitType.Sp, 18);
        button.SetTypeface(Typeface.Monospace, TypefaceStyle.Bold);
        button.SetTextColor((filled ? palette.Background : palette.Lit).ToPlatform());
        button.Background = Box(filled ? palette.Lit : Colors.Transparent, palette.Lit, 2);
        button.StateListAnimator = null;
        button.Click += (_, _) => click();
        return button;
    }

    GradientDrawable Box(Color fill, Color stroke, int strokeDp)
    {
        var box = new GradientDrawable();
        box.SetColor(fill.ToPlatform());
        box.SetStroke(Dp(strokeDp), stroke.ToPlatform());
        box.SetCornerRadius(Dp(6));
        return box;
    }

    static LinearLayout.LayoutParams Wrap() => new(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);

    static LinearLayout.LayoutParams Weighted(int start = 0, int end = 0)
    {
        var p = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1);
        p.SetMargins(start, 0, end, 0);
        return p;
    }

    int Dp(float dp) => (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, Resources!.DisplayMetrics);
}
