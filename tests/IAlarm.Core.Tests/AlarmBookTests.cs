using IAlarm.Core;

namespace IAlarm.Core.Tests;

public class AlarmBookTests
{
    static readonly DateTime Now = new(2026, 11, 14, 18, 0, 30);

    [Fact]
    public void Add_DropsSecondsAndBlankLabels()
    {
        var list = new List<Alarm>();
        var alarm = AlarmBook.Add(list, new DateTime(2026, 11, 15, 7, 30, 45), "  ");
        Assert.Equal(new DateTime(2026, 11, 15, 7, 30, 0), alarm.At);
        Assert.Null(alarm.Label);
        Assert.True(alarm.Enabled);
        Assert.Single(list);
    }

    [Fact]
    public void Chronological_OrdersByTime()
    {
        var list = new List<Alarm>();
        AlarmBook.Add(list, Now.AddDays(2), "c");
        AlarmBook.Add(list, Now.AddHours(1), "a");
        AlarmBook.Add(list, Now.AddDays(1), "b");
        Assert.Equal(["a", "b", "c"], AlarmBook.Chronological(list).Select(a => a.Label));
    }

    [Fact]
    public void Update_TurnsOnAndClearsSnooze()
    {
        var list = new List<Alarm>();
        var id = AlarmBook.Add(list, Now.AddHours(1), null).Id;
        list[0] = list[0] with { Enabled = false, SnoozedUntil = Now };
        Assert.True(AlarmBook.Update(list, id, Now.AddHours(3), " Gym "));
        Assert.True(list[0].Enabled);
        Assert.Null(list[0].SnoozedUntil);
        Assert.Equal("Gym", list[0].Label);
    }

    [Fact]
    public void SetEnabled_RefusesPastAlarm()
    {
        var list = new List<Alarm>();
        var id = AlarmBook.Add(list, Now.AddHours(-1), null).Id;
        list[0] = list[0] with { Enabled = false };
        Assert.False(AlarmBook.SetEnabled(list, id, true, Now));
        Assert.False(list[0].Enabled);
        Assert.True(AlarmBook.SetEnabled(list, id, false, Now));
    }

    [Fact]
    public void Delete_RemovesOnlyThatAlarm()
    {
        var list = new List<Alarm>();
        var id = AlarmBook.Add(list, Now.AddHours(1), "x").Id;
        AlarmBook.Add(list, Now.AddHours(2), "y");
        Assert.True(AlarmBook.Delete(list, id));
        Assert.Equal("y", Assert.Single(list).Label);
    }

    [Fact]
    public void NextTrigger_SkipsOffAlarmsAndUsesSnooze()
    {
        var list = new List<Alarm>();
        AlarmBook.Add(list, Now.AddHours(1), null);
        AlarmBook.Add(list, Now.AddHours(3), null);
        list[0] = list[0] with { Enabled = false };
        Assert.Equal(AlarmBook.ToMinute(Now.AddHours(3)), AlarmBook.NextTrigger(list));

        list[1] = list[1] with { SnoozedUntil = Now.AddMinutes(5) };
        Assert.Equal(Now.AddMinutes(5), AlarmBook.NextTrigger(list));
        Assert.Null(AlarmBook.NextTrigger([]));
    }

    [Fact]
    public void TakeDue_RingsDueAlarmsAndTurnsThemOff()
    {
        var list = new List<Alarm>();
        AlarmBook.Add(list, Now.AddMinutes(-1), "due");
        AlarmBook.Add(list, Now.AddHours(1), "later");
        var due = AlarmBook.TakeDue(list, Now);
        Assert.Equal("due", Assert.Single(due).Label);
        Assert.False(list[0].Enabled);
        Assert.True(list[1].Enabled);
        Assert.Empty(AlarmBook.TakeDue(list, Now));
    }

    [Fact]
    public void Snooze_RingsAgainTenMinutesLater()
    {
        var list = new List<Alarm>();
        var alarm = AlarmBook.Add(list, Now.AddMinutes(-1), null);
        AlarmBook.TakeDue(list, Now);
        AlarmBook.Snooze(list, [alarm.Id], Now);
        Assert.True(list[0].Enabled);
        Assert.Equal(new DateTime(2026, 11, 14, 18, 10, 30), list[0].Trigger);
        Assert.Empty(AlarmBook.TakeDue(list, Now.AddMinutes(9)));
        Assert.Single(AlarmBook.TakeDue(list, Now.AddMinutes(10)));
    }

    [Fact]
    public void ExpireMissed_OnlyTurnsOffLongMissedAlarms()
    {
        var list = new List<Alarm>();
        AlarmBook.Add(list, Now.AddHours(-2), "old");
        AlarmBook.Add(list, Now.AddMinutes(-10), "recent");
        var missed = AlarmBook.ExpireMissed(list, Now);
        Assert.Equal("old", Assert.Single(missed).Label);
        Assert.False(list[0].Enabled);
        Assert.True(list[1].Enabled); // still rings, a little late
    }

    [Fact]
    public void DefaultTime_IsAboutAnHourAheadOnFiveMinuteMark()
    {
        Assert.Equal(new DateTime(2026, 11, 14, 19, 0, 0), AlarmBook.DefaultTime(new DateTime(2026, 11, 14, 18, 3, 10)));
        Assert.Equal(new DateTime(2026, 11, 15, 0, 55, 0), AlarmBook.DefaultTime(new DateTime(2026, 11, 14, 23, 58, 0)));
    }
}
