using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IAlarm.Core;
using IAlarm.Services;

namespace IAlarm.ViewModels;

/// <summary>Add or edit one alarm: date, time and an optional label.</summary>
public sealed partial class AlarmEditorViewModel(AlarmRepository repo) : ObservableObject, IQueryAttributable
{
    string? id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewText), nameof(CountdownText))]
    DateTime date;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewText), nameof(CountdownText))]
    TimeSpan time;

    [ObservableProperty]
    string label = "";

    [ObservableProperty]
    bool isExisting;

    public DateTime Today => DateTime.Today;

    DateTime At => Date.Date + Time;

    public string PreviewText => TimeText.Clock(At);

    public string CountdownText => At > DateTime.Now ? "RINGS " + TimeText.Countdown(At - DateTime.Now) : "THIS TIME HAS PASSED";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        id = query.TryGetValue("id", out var value) ? value as string : null;
        var alarm = id is null ? null : repo.Find(id);
        IsExisting = alarm is not null;
        var at = alarm?.At ?? AlarmBook.DefaultTime(DateTime.Now);
        Date = at.Date;
        Time = at.TimeOfDay;
        Label = alarm?.Label ?? "";
    }

    [RelayCommand]
    async Task Save()
    {
        var at = AlarmBook.ToMinute(At);
        if (at <= DateTime.Now)
        {
            await Shell.Current.DisplayAlert("Pick a later time", "This date and time has already passed.", "OK");
            return;
        }

        if (id is not null && IsExisting)
            repo.Change(list => AlarmBook.Update(list, id, at, Label));
        else
            repo.Change(list => AlarmBook.Add(list, at, Label));

        await SetupCheck.AskForNotificationsAsync();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    static Task Cancel() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    async Task Delete()
    {
        if (id is null || !await Shell.Current.DisplayAlert("Delete alarm?", $"{PreviewText}, {TimeText.Date(At)}", "Delete", "Cancel"))
            return;
        repo.Change(list => AlarmBook.Delete(list, id));
        await Shell.Current.GoToAsync("..");
    }
}
