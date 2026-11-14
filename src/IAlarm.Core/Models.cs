using System.Text.Json.Serialization;

namespace IAlarm.Core;

/// <summary>A one-off alarm at a local date and time.</summary>
public sealed record Alarm
{
    public required string Id { get; init; }

    /// <summary>Local wall-clock time. It stays at the same clock time if the time zone changes.</summary>
    public DateTime At { get; init; }

    public string? Label { get; init; }

    public bool Enabled { get; init; } = true;

    /// <summary>Set while snoozed: the alarm rings again at this time instead of <see cref="At"/>.</summary>
    public DateTime? SnoozedUntil { get; init; }

    /// <summary>When the alarm rings next, if it is on.</summary>
    [JsonIgnore]
    public DateTime Trigger => SnoozedUntil ?? At;
}

public sealed class AlarmData
{
    public const string FormatName = "ialarm";

    public string? Format { get; set; }

    public List<Alarm> Alarms { get; set; } = [];
}
