using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IAlarm.Core;

namespace IAlarm.ViewModels;

public sealed partial class AlarmRowViewModel : ObservableObject
{
    readonly AlarmsViewModel owner;
    readonly Alarm alarm;

    public AlarmRowViewModel(AlarmsViewModel owner, Alarm alarm, DateTime now)
    {
        this.owner = owner;
        this.alarm = alarm;
        enabled = alarm.Enabled;
        RefreshStatus(now);
    }

    public string Id => alarm.Id;
    public string TimeText => Core.TimeText.Clock(alarm.At);
    public string DateText => Core.TimeText.Date(alarm.At);
    public string? Label => alarm.Label;
    public bool HasLabel => alarm.Label is not null;
    public double Dim => Enabled ? 1 : 0.4;

    bool enabled;

    /// <summary>The switch. Turning it on is refused for an alarm whose time has passed; the list then reloads.</summary>
    public bool Enabled
    {
        get => enabled;
        set
        {
            if (!SetProperty(ref enabled, value))
                return;
            OnPropertyChanged(nameof(Dim));
            owner.Toggle(this, value);
        }
    }

    [ObservableProperty]
    string status = "";

    public void RefreshStatus(DateTime now) => Status = alarm switch
    {
        { Enabled: true, SnoozedUntil: { } until } => "SNOOZED UNTIL " + Core.TimeText.Clock(until),
        { Enabled: true } when alarm.At > now => Core.TimeText.Countdown(alarm.At - now),
        { Enabled: true } => "DUE NOW",
        _ when alarm.At <= now => "PASSED",
        _ => "OFF",
    };

    [RelayCommand]
    Task Open() => owner.OpenAsync(this);

    [RelayCommand]
    Task Delete() => owner.DeleteAsync(this);
}
