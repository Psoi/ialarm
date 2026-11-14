using System.Globalization;

namespace IAlarm.Core;

public static class TimeText
{
    /// <summary>mm:ss.cc, or hh:mm:ss.cc from one hour on.</summary>
    public static string Stopwatch(long ms)
    {
        ms = Math.Max(0, ms);
        var centis = ms / 10 % 100;
        var seconds = ms / 1000 % 60;
        var minutes = ms / 60_000 % 60;
        var hours = ms / 3_600_000;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours:00}:{minutes:00}:{seconds:00}.{centis:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes:00}:{seconds:00}.{centis:00}");
    }

    /// <summary>24-hour HH:mm. With <paramref name="colonOn"/> false the colon is drawn unlit (';' for the segment display).</summary>
    public static string Clock(DateTime t, bool colonOn = true) =>
        t.ToString(colonOn ? "HH:mm" : "HH;mm", CultureInfo.InvariantCulture);

    public static string Date(DateTime t) => t.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();

    /// <summary>"IN 2 D 3 H", "IN 5 H 12 MIN", "IN 7 MIN", "IN LESS THAN A MINUTE".</summary>
    public static string Countdown(TimeSpan left)
    {
        if (left < TimeSpan.FromMinutes(1))
            return "IN LESS THAN A MINUTE";
        // Round up so an alarm at 07:30 seen at 07:28:10 says 2 minutes, not 1.
        var minutes = (long)Math.Ceiling(left.TotalMinutes);
        var days = minutes / 1440;
        var hours = minutes / 60 % 24;
        var mins = minutes % 60;
        if (days > 0)
            return hours > 0 ? $"IN {days} D {hours} H" : $"IN {days} D";
        if (hours > 0)
            return mins > 0 ? $"IN {hours} H {mins} MIN" : $"IN {hours} H";
        return $"IN {mins} MIN";
    }
}
