using ChurchAttendance.Tests.Infrastructure;

namespace ChurchAttendance.Tests.Common;

public class ChurchClockTests
{
    [Fact]
    public void Today_SaturdayEveningInMontreal_IsSaturdayEvenThoughUtcIsSunday()
    {
        var clock = FixedTimeProvider.ChurchClockAt(FixedTimeProvider.SaturdayEveningInMontreal);

        Assert.Equal(new DateOnly(2026, 10, 24), clock.Today);
        Assert.Equal(new DateTime(2026, 10, 24, 21, 30, 0), clock.LocalNow);
    }

    [Fact]
    public void MostRecentSunday_OnSaturday_IsPreviousSunday()
    {
        var clock = FixedTimeProvider.ChurchClockAt(FixedTimeProvider.SaturdayEveningInMontreal);

        Assert.Equal(new DateOnly(2026, 10, 18), clock.MostRecentSunday);
    }

    [Fact]
    public void MostRecentSunday_OnSundayMorning_IsToday()
    {
        // Sunday 2026-10-25 at 10:00 EDT.
        var clock = FixedTimeProvider.ChurchClockAt(new DateTimeOffset(2026, 10, 25, 14, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 10, 25), clock.MostRecentSunday);
    }

    [Fact]
    public void ToLocal_And_ToUtc_RoundTrip_AcrossDaylightSavingChange()
    {
        var clock = FixedTimeProvider.ChurchClockAt(FixedTimeProvider.SaturdayEveningInMontreal);

        // 10:00 on a summer Sunday (EDT, UTC-4) and a winter Sunday (EST, UTC-5).
        var summerUtc = new DateTime(2026, 7, 5, 14, 0, 0, DateTimeKind.Utc);
        var winterUtc = new DateTime(2026, 12, 6, 15, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 7, 5, 10, 0, 0), clock.ToLocal(summerUtc));
        Assert.Equal(new DateTime(2026, 12, 6, 10, 0, 0), clock.ToLocal(winterUtc));
        Assert.Equal(winterUtc, clock.ToUtc(new DateTime(2026, 12, 6, 10, 0, 0)));
    }
}
