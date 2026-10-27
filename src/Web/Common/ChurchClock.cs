namespace ChurchAttendance.Common;

// The app runs on Fly.io, where the server clock is UTC, but every date the church cares about
// ("today's service", "this year's Sainte Cène", a check-in time shown on screen) is a date or
// time in the church's own time zone. DateTime.Now / DateTime.Today / ToLocalTime() silently
// use the *server's* zone, so a Saturday-evening check-in in Montréal (after 20:00 EDT) was
// being filed under Sunday's service. All "what day/time is it for the church" questions go
// through this class instead.
public sealed class ChurchClock(TimeProvider timeProvider, TimeZoneInfo timeZone)
{
    public const string DefaultTimeZoneId = "America/Toronto";

    public TimeZoneInfo TimeZone { get; } = timeZone;

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateTime LocalNow => ToLocal(UtcNow);

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    // Today if it's Sunday, otherwise the previous Sunday — the default service the reports open on.
    public DateOnly MostRecentSunday
    {
        get
        {
            var today = Today;
            return today.AddDays(-(int)today.DayOfWeek);
        }
    }

    // Converts a UTC timestamp (as stored in Postgres timestamptz columns) to the church's
    // local wall-clock time for display or export.
    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    // Converts a local wall-clock time in the church's zone to UTC (mainly for seeding data).
    public DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZone);
}

public static class ChurchClockSetup
{
    public static IServiceCollection AddChurchClock(this IServiceCollection services, IConfiguration configuration)
    {
        var timeZoneId = configuration["ChurchTimeZone"] ?? ChurchClock.DefaultTimeZoneId;

        // Resolved eagerly so a missing time zone database fails the deploy at startup
        // (caught on staging) instead of on the first check-in.
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new ChurchClock(sp.GetRequiredService<TimeProvider>(), timeZone));
        return services;
    }
}
