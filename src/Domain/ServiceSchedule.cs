namespace ChurchAttendance.Domain;

// One quarter's Sunday service schedule, the "Service dominical — Du 4 octobre au 27 décembre"
// sheet: who leads and who preaches each Sunday of the quarter (calendar quarters, Q4 =
// October to December).
public class ServiceSchedule
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }

    // The "N.B." line at the bottom of the sheet (who to call if a date doesn't suit).
    public string? FooterNote { get; set; }

    public DateTime UpdatedAt { get; set; }
    public List<ScheduledService> Services { get; set; } = [];

    // Every Sunday of the quarter: 13, or 14 when the quarter starts and ends on a Sunday.
    public static IReadOnlyList<DateOnly> SundaysOf(int year, int quarter)
    {
        if (quarter is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(quarter), quarter, "A quarter is 1 to 4.");
        }

        var start = new DateOnly(year, (quarter - 1) * 3 + 1, 1);
        var end = start.AddMonths(3);
        var sunday = start.AddDays(((int)DayOfWeek.Sunday - (int)start.DayOfWeek + 7) % 7);

        var sundays = new List<DateOnly>();
        for (; sunday < end; sunday = sunday.AddDays(7))
        {
            sundays.Add(sunday);
        }
        return sundays;
    }

    public static int QuarterOf(DateOnly date) => (date.Month - 1) / 3 + 1;
}

// One Sunday of a ServiceSchedule. Names are free text, not members: guest pastors
// ("Rév. ..."), groups ("La jeunesse") and titles ("Sr", "Fr") all appear on the sheet.
public class ScheduledService
{
    public int Id { get; set; }
    public int ServiceScheduleId { get; set; }
    public DateOnly Date { get; set; }
    public string? Leader { get; set; }
    public string? Preacher { get; set; }
}
