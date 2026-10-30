using ChurchAttendance.Domain;

namespace ChurchAttendance.Application.Announcements;

// How an announcement is written on its slide, in French: the bold first line ("Samedi 31
// octobre 2026, à 18h00 :") and the heading above it. Spelled out here rather than taken from a
// CultureInfo so the slides read the same whatever the server's globalization data.
public static class AnnouncementTexts
{
    public const string ExternalHeading = "Annonce externe";

    private static readonly string[] DayNames = ["dimanche", "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi"];

    private static readonly string[] MonthNames =
        ["janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre"];

    public static string KindLabel(AnnouncementKind kind) => kind switch
    {
        AnnouncementKind.Recurring => "Activités de la semaine",
        AnnouncementKind.Event => "Événements",
        AnnouncementKind.Pastoral => "Annonces pastorales",
        AnnouncementKind.External => "Annonces externes",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    // The bold line that opens the announcement, ending with " :"; none for a notice with
    // neither date nor schedule (a pastoral one, usually).
    public static string? WhenLine(Announcement announcement)
    {
        if (announcement.EventDate is { } start)
        {
            var dates = announcement.EndDate is { } end && end != start ? Range(start, end) : Capitalize(LongDate(start));
            return string.IsNullOrWhiteSpace(announcement.EventTime) ? $"{dates} :" : $"{dates}, {announcement.EventTime} :";
        }
        return string.IsNullOrWhiteSpace(announcement.Schedule) ? null : $"{announcement.Schedule} :";
    }

    // The heading over the announcement on its slide: its own, else "Annonce externe" for
    // another church's, else none (the month's theme is said once, on the title slide).
    public static string? Heading(Announcement announcement)
    {
        if (!string.IsNullOrWhiteSpace(announcement.Heading))
        {
            return announcement.Heading;
        }
        return announcement.Kind == AnnouncementKind.External ? ExternalHeading : null;
    }

    public static string MonthName(int month) => MonthNames[month - 1];

    // "samedi 31 octobre 2026"
    private static string LongDate(DateOnly date) =>
        $"{DayNames[(int)date.DayOfWeek]} {Day(date)} {MonthNames[date.Month - 1]} {date.Year}";

    // "Du vendredi 6 au dimanche 8 novembre 2026": the month and year once when they are shared.
    private static string Range(DateOnly start, DateOnly end)
    {
        var first = start.Year != end.Year ? LongDate(start)
            : start.Month != end.Month ? $"{DayNames[(int)start.DayOfWeek]} {Day(start)} {MonthNames[start.Month - 1]}"
            : $"{DayNames[(int)start.DayOfWeek]} {Day(start)}";
        return $"Du {first} au {LongDate(end)}";
    }

    private static string Day(DateOnly date) => date.Day == 1 ? "1er" : date.Day.ToString();

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
