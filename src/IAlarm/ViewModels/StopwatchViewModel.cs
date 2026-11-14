using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IAlarm.Core;
using IAlarm.Services;

namespace IAlarm.ViewModels;

/// <summary>
/// The Stopwatch tab. The time always comes from <see cref="StopwatchStore"/>'s timestamps, so it is right after
/// rotating, switching apps or the screen going off; the timer here only repaints while the tab is visible.
/// </summary>
public sealed partial class StopwatchViewModel : ObservableObject
{
    IDispatcherTimer? timer;
    bool visible;
    StopwatchState state;

    [ObservableProperty]
    string displayText = TimeText.Stopwatch(0);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand), nameof(ResetCommand))]
    bool running;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    bool hasTime;

    public StopwatchViewModel()
    {
        StopwatchStore.Changed += () => MainThread.BeginInvokeOnMainThread(Refresh);
        App.Foreground += () =>
        {
            if (visible)
                Refresh();
        };
        App.Background += () => timer?.Stop();
        Refresh();
    }

    public void Appearing()
    {
        visible = true;
        Refresh();
    }

    public void Disappearing()
    {
        visible = false;
        timer?.Stop();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    static void Start() => StopwatchStore.Start(Platform.AppContext);

    bool CanStart() => !Running;

    [RelayCommand(CanExecute = nameof(CanStop))]
    static void Stop() => StopwatchStore.Stop(Platform.AppContext);

    bool CanStop() => Running;

    [RelayCommand(CanExecute = nameof(CanReset))]
    static void Reset() => StopwatchStore.Reset(Platform.AppContext);

    bool CanReset() => HasTime || Running;

    void Refresh()
    {
        state = StopwatchStore.Load(Platform.AppContext);
        Running = state.Running;
        Paint();
        if (Running && visible)
        {
            timer ??= CreateTimer();
            timer.Start();
        }
        else
        {
            timer?.Stop();
        }
    }

    void Paint()
    {
        var elapsed = state.Elapsed(StopwatchStore.Now);
        DisplayText = TimeText.Stopwatch(elapsed);
        HasTime = elapsed > 0;
    }

    IDispatcherTimer CreateTimer()
    {
        var t = Application.Current!.Dispatcher.CreateTimer();
        t.Interval = TimeSpan.FromMilliseconds(31);
        t.Tick += (_, _) => Paint();
        return t;
    }
}
