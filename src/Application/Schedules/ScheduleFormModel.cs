namespace ChurchAttendance.Application.Schedules;

// A quarter as the Programmation page edits it: one row per Sunday, in date order.
public class ScheduleFormModel
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public List<ScheduleRow> Rows { get; set; } = [];
    public string? FooterNote { get; set; }

    // False until the quarter is saved for the first time.
    public bool Exists { get; set; }

    public int MissingCount => Rows.Count(r => string.IsNullOrWhiteSpace(r.Leader) || string.IsNullOrWhiteSpace(r.Preacher));
}

public class ScheduleRow
{
    public DateOnly Date { get; set; }
    public string? Leader { get; set; }
    public string? Preacher { get; set; }
}
