using ChurchAttendance.Application.Schedules;

namespace ChurchAttendance.Tests.Common;

public class ScheduleDatesTests
{
    [Fact]
    public void SundaysOf_Q4_2026_IsTheSheetsThirteenSundays()
    {
        var sundays = ServiceSchedule.SundaysOf(2026, 4);

        Assert.Equal(13, sundays.Count);
        Assert.Equal(new DateOnly(2026, 10, 4), sundays[0]);
        Assert.Equal(new DateOnly(2026, 12, 27), sundays[^1]);
        Assert.All(sundays, d => Assert.Equal(DayOfWeek.Sunday, d.DayOfWeek));
    }

    [Fact]
    public void SundaysOf_AQuarterStartingAndEndingOnSunday_HasFourteen()
    {
        // July 1st and September 30th 2029 are both Sundays.
        var sundays = ServiceSchedule.SundaysOf(2029, 3);

        Assert.Equal(14, sundays.Count);
        Assert.Equal(new DateOnly(2029, 7, 1), sundays[0]);
        Assert.Equal(new DateOnly(2029, 9, 30), sundays[^1]);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(9, 3)]
    [InlineData(10, 4)]
    [InlineData(12, 4)]
    public void QuarterOf_CalendarQuarters(int month, int quarter)
    {
        Assert.Equal(quarter, ServiceSchedule.QuarterOf(new DateOnly(2026, month, 15)));
    }

    [Theory]
    [InlineData(2026, 10, 4, "4 oct.")]
    [InlineData(2026, 11, 1, "1er nov.")]
    [InlineData(2026, 12, 27, "27 déc.")]
    [InlineData(2027, 2, 7, "7 févr.")]
    public void ShortDate_IsWrittenLikeTheSheet(int year, int month, int day, string expected)
    {
        Assert.Equal(expected, ScheduleTexts.ShortDate(new DateOnly(year, month, day)));
    }

    [Fact]
    public void QuarterPeriod_IsTheSheetsSubtitle()
    {
        Assert.Equal("Du 4 octobre au 27 décembre 2026", ScheduleTexts.QuarterPeriod(2026, 4));
    }

    [Fact]
    public void Period_AcrossTwoYears_GivesBothYears()
    {
        Assert.Equal("Du 27 décembre 2026 au 3 janvier 2027",
            ScheduleTexts.Period(new DateOnly(2026, 12, 27), new DateOnly(2027, 1, 3)));
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(11, 1)]
    [InlineData(12, 2)]
    [InlineData(1, 0)]
    public void MonthInQuarter_GivesTheSheetsColour(int month, int expected)
    {
        Assert.Equal(expected, ScheduleTexts.MonthInQuarter(new DateOnly(2026, month, 1)));
    }
}
