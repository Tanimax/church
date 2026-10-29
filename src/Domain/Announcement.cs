namespace ChurchAttendance.Domain;

// The kinds of announcement read on Sunday, in the order they come in the announcements.
public enum AnnouncementKind
{
    // Every week the same ("Mercredi soir de 19h00 à 21h15 : Réunion de prière").
    Recurring = 0,

    // On a date; leaves the announcements once that date has passed.
    Event = 1,

    // Standing pastoral notices ("Les gens malades... contactez pasteur...").
    Pastoral = 2,

    // Another church's invitation, under an "Annonce externe" heading.
    External = 3,
}

// One announcement of the catalogue the secretariat keeps: entered once, then shown every
// Sunday until it is archived or, for a dated one, until its date has passed.
public class Announcement
{
    public int Id { get; set; }
    public AnnouncementKind Kind { get; set; }

    // Its own heading on the slide ("2e jeûne annuel"); without one, the month's theme
    // (or "Annonce externe" for an external one) heads it.
    public string? Heading { get; set; }

    // When a recurring announcement happens, as said aloud: "Mardi et vendredi de 12h00 à 12h30".
    public string? Schedule { get; set; }

    // When a dated announcement happens: one day, or several (EndDate), and the hours as said
    // aloud ("à 18h00", "de 8h30 à 14h00").
    public DateOnly? EventDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? EventTime { get; set; }

    // The line in red on the slide: what happens ("Répétition de la chorale").
    public required string Title { get; set; }

    // The lines below it ("Sujet : ...", "Tél. ...", "N.B. : ...").
    public List<AnnouncementDetail> Details { get; set; } = [];

    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // The last day it is worth announcing: its (last) date, or none for a standing one.
    public DateOnly? LastDay => EndDate ?? EventDate;

    // Whether it belongs in the announcements read that Sunday: not archived, and not over.
    // An event on the Sunday itself (an afternoon meeting) is still announced that morning.
    public bool IsShownOn(DateOnly sunday) => !IsArchived && (LastDay is not { } last || sunday <= last);
}

// A line under an announcement's title, with an optional label written before it
// ("Sujet", "Tél.", "Prédicateur", "Horaire", "N.B.").
public class AnnouncementDetail
{
    public string? Label { get; set; }
    public required string Text { get; set; }
}

// The month's theme, at the top of the announcement slides ("Octobre : Mois d'actions de grâces").
public class MonthlyTheme
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public required string Text { get; set; }
}
