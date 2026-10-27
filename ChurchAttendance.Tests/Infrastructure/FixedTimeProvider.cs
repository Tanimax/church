using ChurchAttendance.Common;

namespace ChurchAttendance.Tests.Infrastructure;

// A TimeProvider frozen at a given instant, so date-dependent behavior ("today's service",
// local display times) can be tested deterministically, regardless of when or where CI runs.
public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;

    // Saturday 2026-10-24 at 21:30 in Montréal (EDT, UTC-4) — already Sunday 01:30 in UTC.
    public static readonly DateTimeOffset SaturdayEveningInMontreal = new(2026, 10, 25, 1, 30, 0, TimeSpan.Zero);

    public static ChurchClock ChurchClockAt(DateTimeOffset utcNow) =>
        new(new FixedTimeProvider(utcNow), TimeZoneInfo.FindSystemTimeZoneById(ChurchClock.DefaultTimeZoneId));
}
