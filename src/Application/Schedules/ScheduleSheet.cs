namespace ChurchAttendance.Application.Schedules;

// What the printed "Service dominical" sheet shows, already worded: the PDF renderer only lays
// it out. Built from the saved quarter (ScheduleService.GetAsync).
public sealed record ScheduleSheet(string Title, string Period, IReadOnlyList<ScheduleSheetRow> Rows, string? FooterNote)
{
    public static ScheduleSheet From(ScheduleFormModel quarter) => new(
        "Service dominical",
        ScheduleTexts.QuarterPeriod(quarter.Year, quarter.Quarter),
        quarter.Rows.Select(r => new ScheduleSheetRow(
            r.Date.Day.ToString(),
            r.Date.Day == 1 ? "er" : "",
            ScheduleTexts.MonthAbbreviation(r.Date),
            ScheduleTexts.MonthInQuarter(r.Date),
            r.Leader ?? "",
            r.Preacher ?? "")).ToList(),
        FooterLine(quarter.FooterNote));

    // The sheet prints the note after "N.B.", whether or not it was typed with it.
    private static string? FooterLine(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }
        var text = note.Trim();
        return text.StartsWith("N.B.", StringComparison.OrdinalIgnoreCase) ? text : $"N.B. {text}";
    }
}

// One Sunday: "1" + superscript "er" + "nov.", in the colour of its month (0, 1 or 2).
public sealed record ScheduleSheetRow(string Day, string DaySuffix, string Month, int MonthInQuarter, string Leader, string Preacher);
