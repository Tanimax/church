using ChurchAttendance.Domain;

namespace ChurchAttendance.Application.Schedules;

// How the schedule sheet writes dates, in French: "4 oct.", "1er nov.",
// "Du 4 octobre au 27 décembre 2026". Spelled out here rather than taken from a CultureInfo
// so the sheet reads the same whatever the server's globalization data.
public static class ScheduleTexts
{
    private static readonly string[] MonthNames =
        ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre"];

    private static readonly string[] MonthAbbreviations =
        ["janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc."];

    public static string ShortDate(DateOnly date) => $"{Day(date)} {MonthAbbreviations[date.Month - 1]}";

    // "Du 4 octobre au 27 décembre 2026" (the year once, unless the range spans two years).
    public static string Period(DateOnly first, DateOnly last) =>
        first.Year == last.Year
            ? $"Du {Day(first)} {MonthNames[first.Month - 1]} au {Day(last)} {MonthNames[last.Month - 1]} {last.Year}"
            : $"Du {LongDate(first)} au {LongDate(last)}";

    public static string QuarterPeriod(int year, int quarter)
    {
        var sundays = ServiceSchedule.SundaysOf(year, quarter);
        return Period(sundays[0], sundays[^1]);
    }

    // 0, 1 or 2: the date's month within its quarter. The sheet colours each month's rows
    // (green, pink, blue), the same colours every quarter.
    public static int MonthInQuarter(DateOnly date) => (date.Month - 1) % 3;

    private static string LongDate(DateOnly date) => $"{Day(date)} {MonthNames[date.Month - 1]} {date.Year}";

    private static string Day(DateOnly date) => date.Day == 1 ? "1er" : date.Day.ToString();
}
