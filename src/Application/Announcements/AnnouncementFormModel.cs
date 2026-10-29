using ChurchAttendance.Domain;

namespace ChurchAttendance.Application.Announcements;

// An announcement as the Annonces page edits it. Id 0: a new one.
public class AnnouncementFormModel
{
    public int Id { get; set; }
    public AnnouncementKind Kind { get; set; } = AnnouncementKind.Event;
    public string? Heading { get; set; }
    public string? Schedule { get; set; }
    public DateOnly? EventDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? EventTime { get; set; }
    public string? Title { get; set; }
    public List<AnnouncementDetailRow> Details { get; set; } = [];

    public static AnnouncementFormModel From(Announcement announcement) => new()
    {
        Id = announcement.Id,
        Kind = announcement.Kind,
        Heading = announcement.Heading,
        Schedule = announcement.Schedule,
        EventDate = announcement.EventDate,
        EndDate = announcement.EndDate,
        EventTime = announcement.EventTime,
        Title = announcement.Title,
        Details = announcement.Details.Select(d => new AnnouncementDetailRow { Label = d.Label, Text = d.Text }).ToList(),
    };
}

public class AnnouncementDetailRow
{
    public string? Label { get; set; }
    public string? Text { get; set; }
}
