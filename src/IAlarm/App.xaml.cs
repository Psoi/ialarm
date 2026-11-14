namespace IAlarm;

public partial class App : Application
{
    static string? pendingTab;

    /// <summary>The app came to the front (also after returning from a system settings screen).</summary>
    public static event Action? Foreground;

    /// <summary>The app went behind something else; screens stop their timers.</summary>
    public static event Action? Background;

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        window.Activated += (_, _) => Foreground?.Invoke();
        window.Deactivated += (_, _) => Background?.Invoke();
        return window;
    }

    /// <summary>Switches tab, now or once the shell is up. From notifications: "alarms" or "stopwatch".</summary>
    public static void OpenTab(string tab)
    {
        pendingTab = tab;
        OpenPendingTab();
    }

    public static void OpenPendingTab()
    {
        if (pendingTab is not { } tab || Shell.Current is not AppShell { IsLoaded: true } shell)
            return;
        pendingTab = null;
        shell.Dispatcher.Dispatch(async () => await shell.GoToAsync("//" + tab, animate: false));
    }
}
