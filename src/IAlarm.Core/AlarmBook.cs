namespace IAlarm.Core;

/// <summary>
/// Every change to the alarm list. Alarms are records, so a change replaces the item in the list.
/// An alarm that is on and whose trigger time has passed is due: ringing it turns it off (or snoozing turns it back on).
/// </summary>
public static class AlarmBook
{
    public const int SnoozeMinutes = 10;

    /// <summary>An alarm missed by more than this (the phone was off) is turned off instead of ringing late.</summary>
    public static readonly TimeSpan MissedAfter = TimeSpan.FromMinutes(30);

    /// <summary>The system may wake us a moment early.</summary>
    static readonly TimeSpan Early = TimeSpan.FromSeconds(2);

    public static IEnumerable<Alarm> Chronological(IEnumerable<Alarm> alarms) =>
        alarms.OrderBy(a => a.At)
            .ThenBy(a => a.Label ?? "", StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.Id, StringComparer.Ordinal);

    /// <summary>The earliest trigger of the alarms that are on, which may already be past (then it is due now).</summary>
    public static DateTime? NextTrigger(IEnumerable<Alarm> alarms)
    {
        DateTime? next = null;
        foreach (var alarm in alarms)
            if (alarm.Enabled && (next is null || alarm.Trigger < next))
                next = alarm.Trigger;
        return next;
    }

    public static Alarm Add(List<Alarm> alarms, DateTime at, string? label)
    {
        var alarm = new Alarm { Id = Guid.NewGuid().ToString("N"), At = ToMinute(at), Label = Clean(label) };
        alarms.Add(alarm);
        return alarm;
    }

    /// <summary>Saving an alarm turns it on and cancels any snooze.</summary>
    public static bool Update(List<Alarm> alarms, string id, DateTime at, string? label) =>
        Replace(alarms, id, a => a with { At = ToMinute(at), Label = Clean(label), Enabled = true, SnoozedUntil = null });

    public static bool Delete(List<Alarm> alarms, string id) => alarms.RemoveAll(a => a.Id == id) > 0;

    /// <summary>False, and nothing changes, when turning on an alarm whose time has passed.</summary>
    public static bool SetEnabled(List<Alarm> alarms, string id, bool enabled, DateTime now)
    {
        var index = alarms.FindIndex(a => a.Id == id);
        if (index < 0 || (enabled && alarms[index].At <= now))
            return false;
        alarms[index] = alarms[index] with { Enabled = enabled, SnoozedUntil = null };
        return true;
    }

    /// <summary>The alarms to ring now. They are turned off; snoozing turns them back on.</summary>
    public static List<Alarm> TakeDue(List<Alarm> alarms, DateTime now)
    {
        var due = new List<Alarm>();
        for (var i = 0; i < alarms.Count; i++)
        {
            var alarm = alarms[i];
            if (!alarm.Enabled || alarm.Trigger > now + Early)
                continue;
            due.Add(alarm);
            alarms[i] = alarm with { Enabled = false, SnoozedUntil = null };
        }
        return due;
    }

    /// <summary>After a reboot or clock change: turns off alarms missed too long ago, and returns them.</summary>
    public static List<Alarm> ExpireMissed(List<Alarm> alarms, DateTime now)
    {
        var missed = new List<Alarm>();
        for (var i = 0; i < alarms.Count; i++)
        {
            var alarm = alarms[i];
            if (!alarm.Enabled || alarm.Trigger >= now - MissedAfter)
                continue;
            missed.Add(alarm);
            alarms[i] = alarm with { Enabled = false, SnoozedUntil = null };
        }
        return missed;
    }

    public static void Snooze(List<Alarm> alarms, IEnumerable<string> ids, DateTime now)
    {
        var until = ToSecond(now).AddMinutes(SnoozeMinutes);
        foreach (var id in ids)
            Replace(alarms, id, a => a with { Enabled = true, SnoozedUntil = until });
    }

    /// <summary>A new alarm starts about an hour from now, on a 5-minute mark.</summary>
    public static DateTime DefaultTime(DateTime now)
    {
        var at = ToMinute(now).AddHours(1);
        return at.AddMinutes(-(at.Minute % 5));
    }

    public static DateTime ToMinute(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Kind);

    static DateTime ToSecond(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second, t.Kind);

    static string? Clean(string? label) => string.IsNullOrWhiteSpace(label) ? null : label.Trim();

    static bool Replace(List<Alarm> alarms, string id, Func<Alarm, Alarm> change)
    {
        var index = alarms.FindIndex(a => a.Id == id);
        if (index < 0)
            return false;
        alarms[index] = change(alarms[index]);
        return true;
    }
}
