using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IAlarm.Core;
using IAlarm.Services;
using IAlarm.Views;

namespace IAlarm.ViewModels;

/// <summary>The Alarm tab: a live clock, any setup problems, and the alarms in time order.</summary>
public sealed partial class AlarmsViewModel : ObservableObject
{
    readonly AlarmRepository repo;
    IDispatcherTimer? timer;
    bool visible;
    int lastMinute = -1;
    bool askedForNotifications;

    public ObservableCollection<AlarmRowViewModel> Rows { get; } = [];
    public ObservableCollection<SetupIssueViewModel> Issues { get; } = [];

    [ObservableProperty]
    string clockText = "";

    [ObservableProperty]
    string dateText = "";

    [ObservableProperty]
    string nextText = "";

    public AlarmsViewModel(AlarmRepository repo)
    {
        this.repo = repo;
        AlarmRepository.Changed += () => MainThread.BeginInvokeOnMainThread(Reload);
        App.Foreground += () =>
        {
            if (visible)
                Resume();
        };
        App.Background += StopTimer;
        Reload();
    }

    public async Task AppearingAsync()
    {
        visible = true;
        Resume();
        // An alarm app needs notifications; Android shows its prompt once, on first launch.
        if (!askedForNotifications)
        {
            askedForNotifications = true;
            await SetupCheck.AskForNotificationsAsync();
            RefreshIssues();
        }
    }

    public void Disappearing()
    {
        visible = false;
        StopTimer();
    }

    void Resume()
    {
        RefreshIssues();
        Tick();
        timer ??= CreateTimer();
        timer.Start();
    }

    void StopTimer() => timer?.Stop();

    IDispatcherTimer CreateTimer()
    {
        var t = Application.Current!.Dispatcher.CreateTimer();
        t.Interval = TimeSpan.FromMilliseconds(500);
        t.Tick += (_, _) => Tick();
        return t;
    }

    /// <summary>The clock's colon blinks every half second; countdowns update each minute.</summary>
    void Tick()
    {
        var now = DateTime.Now;
        ClockText = TimeText.Clock(now, colonOn: now.Millisecond < 500);
        DateText = TimeText.Date(now);
        if (now.Minute == lastMinute)
            return;
        lastMinute = now.Minute;
        foreach (var row in Rows)
            row.RefreshStatus(now);
        NextText = AlarmBook.NextTrigger(repo.All) is { } next && next > now
            ? "NEXT ALARM " + TimeText.Countdown(next - now)
            : "";
    }

    void Reload()
    {
        var now = DateTime.Now;
        Rows.Clear();
        foreach (var alarm in AlarmBook.Chronological(repo.All))
            Rows.Add(new AlarmRowViewModel(this, alarm, now));
        lastMinute = -1;
        Tick();
    }

    void RefreshIssues()
    {
        var issues = SetupCheck.Find();
        if (issues.SequenceEqual(Issues.Select(i => i.Issue)))
            return;
        Issues.Clear();
        foreach (var issue in issues)
            Issues.Add(new SetupIssueViewModel(issue));
    }

    [RelayCommand]
    static Task Add() => Shell.Current.GoToAsync(nameof(AlarmEditorPage));

    public Task OpenAsync(AlarmRowViewModel row) =>
        Shell.Current.GoToAsync($"{nameof(AlarmEditorPage)}?id={Uri.EscapeDataString(row.Id)}");

    public async void Toggle(AlarmRowViewModel row, bool enabled)
    {
        var now = DateTime.Now;
        var changed = repo.Change(list => AlarmBook.SetEnabled(list, row.Id, enabled, now));
        if (!changed && enabled)
            await Shell.Current.DisplayAlert("Time has passed", "Edit this alarm to pick a new date and time.", "OK");
    }

    public async Task DeleteAsync(AlarmRowViewModel row)
    {
        var name = row.Label is { } label ? $"{row.TimeText} {label}" : row.TimeText;
        if (!await Shell.Current.DisplayAlert("Delete alarm?", $"{name}, {row.DateText}", "Delete", "Cancel"))
            return;
        repo.Change(list => AlarmBook.Delete(list, row.Id));
    }
}

public sealed partial class SetupIssueViewModel(SetupIssue issue) : ObservableObject
{
    public SetupIssue Issue => issue;
    public string Text => SetupCheck.Describe(issue);

    [RelayCommand]
    Task Fix() => SetupCheck.FixAsync(issue);
}
