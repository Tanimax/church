using System.Text.RegularExpressions;
using ChurchAttendance.Domain;

namespace ChurchAttendance.Application.Announcements;

// The announcement as the simplified form asks for it: "when?" (a date, or every week / no
// date), "another church's?", and the details as plain lines ("Sujet : ..."). It maps to the
// same announcement as before: the kind is worked out rather than picked from a list.
public partial class AnnouncementDraft
{
    public int Id { get; set; }
    public string? Title { get; set; }

    // True: on a date (and possibly until another), leaves the announcements once passed.
    // False: every week ("Mercredi soir de 19h00 à 21h15") or a standing notice with no when.
    public bool IsDated { get; set; } = true;

    public DateOnly? Date { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Time { get; set; }
    public string? Schedule { get; set; }

    // One detail per line; "Label : text" when the line starts with a short label.
    public string? Details { get; set; }

    public bool IsExternal { get; set; }
    public string? Heading { get; set; }

    public AnnouncementKind Kind => (IsExternal, IsDated) switch
    {
        (true, _) => AnnouncementKind.External,
        (false, true) => AnnouncementKind.Event,
        (false, false) when !string.IsNullOrWhiteSpace(Schedule) => AnnouncementKind.Recurring,
        _ => AnnouncementKind.Pastoral,
    };

    public AnnouncementFormModel ToForm() => new()
    {
        Id = Id,
        Kind = Kind,
        Heading = Heading,
        Title = Title,
        EventDate = IsDated ? Date : null,
        EndDate = IsDated ? EndDate : null,
        EventTime = IsDated ? Time : null,
        Schedule = IsDated ? null : Schedule,
        Details = ParseDetails(Details),
    };

    // What the slide would show, for the form's preview.
    public Announcement Preview()
    {
        var form = ToForm();
        return new Announcement
        {
            Kind = form.Kind, Heading = form.Heading, Schedule = form.Schedule, EventDate = form.EventDate,
            EndDate = form.EndDate, EventTime = form.EventTime, Title = form.Title ?? "",
            Details = form.Details.Where(d => !string.IsNullOrWhiteSpace(d.Text))
                .Select(d => new AnnouncementDetail { Label = d.Label, Text = d.Text! }).ToList(),
        };
    }

    public static AnnouncementDraft From(Announcement announcement) => new()
    {
        Id = announcement.Id,
        Title = announcement.Title,
        IsDated = announcement.EventDate is not null || announcement.Kind == AnnouncementKind.Event,
        Date = announcement.EventDate,
        EndDate = announcement.EndDate,
        Time = announcement.EventTime,
        Schedule = announcement.Schedule,
        Details = string.Join("\n", announcement.Details.Select(d => d.Label is null ? d.Text : $"{d.Label} : {d.Text}")),
        IsExternal = announcement.Kind == AnnouncementKind.External,
        Heading = announcement.Heading,
    };

    // "Sujet : 2 Cor. 4 : 16" is labelled "Sujet"; "2 Cor. 4 : 16" is not (a label has no digit
    // and is short), so a verse or a time is never taken for one.
    public static List<AnnouncementDetailRow> ParseDetails(string? text) =>
        (text ?? "")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(line => LabelledLine().Match(line) is { Success: true } m
                ? new AnnouncementDetailRow { Label = m.Groups["label"].Value.Trim(), Text = m.Groups["text"].Value.Trim() }
                : new AnnouncementDetailRow { Text = line })
            .ToList();

    [GeneratedRegex(@"^(?<label>[^:\d]{1,30}?)\s*:\s*(?<text>\S.*)$")]
    private static partial Regex LabelledLine();
}
