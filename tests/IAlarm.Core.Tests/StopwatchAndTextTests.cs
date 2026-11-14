using IAlarm.Core;

namespace IAlarm.Core.Tests;

public class StopwatchAndTextTests
{
    [Fact]
    public void Stopwatch_AccumulatesAcrossPauses()
    {
        var s = StopwatchState.Reset.Start(1000);
        Assert.Equal(500, s.Elapsed(1500));
        s = s.Stop(2000);
        Assert.False(s.Running);
        Assert.Equal(1000, s.Elapsed(99_999));
        s = s.Start(5000);
        Assert.Equal(1250, s.Elapsed(5250));
        Assert.Equal(s, s.Start(6000)); // already running
    }

    [Fact]
    public void Stopwatch_AfterRebootStopsAtAccumulated()
    {
        var s = new StopwatchState(true, 700, 50_000).AfterClockReset(1000);
        Assert.False(s.Running);
        Assert.Equal(700, s.Elapsed(2000));
    }

    [Theory]
    [InlineData(0, "00:00.00")]
    [InlineData(1234, "00:01.23")]
    [InlineData(59 * 60_000 + 59_990, "59:59.99")]
    [InlineData(3_600_000, "01:00:00.00")]
    [InlineData(3_723_450, "01:02:03.45")]
    public void Stopwatch_Format(long ms, string expected) => Assert.Equal(expected, TimeText.Stopwatch(ms));

    [Fact]
    public void Clock_UsesUnlitColonWhenBlinking()
    {
        var t = new DateTime(2026, 1, 1, 7, 5, 0);
        Assert.Equal("07:05", TimeText.Clock(t));
        Assert.Equal("07;05", TimeText.Clock(t, colonOn: false));
    }

    [Fact]
    public void Countdown_RoundsUp()
    {
        Assert.Equal("IN LESS THAN A MINUTE", TimeText.Countdown(TimeSpan.FromSeconds(30)));
        Assert.Equal("IN 2 MIN", TimeText.Countdown(TimeSpan.FromSeconds(110)));
        Assert.Equal("IN 1 H", TimeText.Countdown(TimeSpan.FromMinutes(60)));
        Assert.Equal("IN 5 H 12 MIN", TimeText.Countdown(new TimeSpan(5, 12, 0)));
        Assert.Equal("IN 2 D 3 H", TimeText.Countdown(new TimeSpan(2, 3, 0, 0)));
    }

    [Fact]
    public void SevenSegment_Masks()
    {
        Assert.Equal(SevenSegment.All, SevenSegment.Mask('8'));
        Assert.Equal(SevenSegment.B | SevenSegment.C, SevenSegment.Mask('1'));
        Assert.Equal(0, SevenSegment.Mask(' '));
        // Every digit lights a distinct pattern.
        Assert.Equal(10, "0123456789".Select(SevenSegment.Mask).Distinct().Count());
        Assert.False(SevenSegment.IsDigitCell(':'));
        Assert.True(SevenSegment.IsDigitCell(' '));
    }

    [Fact]
    public void Store_RoundTripsAndKeepsCorruptFile()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "alarms.json");
            var store = new AlarmStore(path);
            Assert.Empty(store.Load().Alarms);

            var list = new List<Alarm>();
            AlarmBook.Add(list, new DateTime(2026, 12, 1, 6, 45, 0), "Train");
            store.Save(list);
            var loaded = Assert.Single(store.Load().Alarms);
            Assert.Equal(list[0], loaded);

            File.WriteAllText(path, "{ not json");
            Assert.Empty(store.Load().Alarms);
            Assert.Single(Directory.GetFiles(dir.FullName, "alarms.json.corrupt-*"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
