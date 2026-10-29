using ChurchAttendance.Domain;

namespace ChurchAttendance.Application.Announcements;

// What the projected "Les annonces de la semaine" deck shows, already worded and spread over
// slides: the PowerPoint renderer only lays it out on the church's template. The month's theme
// is on the title slide only, not repeated over every announcement.
public sealed record AnnouncementDeck(DateOnly Sunday, AnnouncementDate Date, string? MonthlyTheme, IReadOnlyList<DeckSlide> Slides)
{
    public const string Title = "LES ANNONCES DE LA SEMAINE";
    public const string BirthdaysHeading = "LES ANNIVERSAIRES DE LA SEMAINE";

    // A 36-point bold line on the 9-inch text box holds about this many characters, and the
    // box about this many lines: enough to spread announcements the way the secretariat did.
    // The renderer also lets PowerPoint shrink a slide whose text overflows anyway.
    public const int CharactersPerLine = 38;
    public const int LinesPerSlide = 8;

    // Names are bigger and on one line each.
    public const int NamesPerSlide = 7;

    public static AnnouncementDeck Build(DateOnly sunday, IEnumerable<Announcement> announcements, string? monthlyTheme,
        IEnumerable<string> birthdayNames)
    {
        var slides = new List<DeckSlide>();
        List<DeckParagraph>? current = null;
        string? currentHeading = null;
        var currentLines = 0;

        void Start(string? heading)
        {
            current = [];
            currentHeading = heading;
            currentLines = 0;
            slides.Add(new DeckSlide(heading, current));
        }

        foreach (var announcement in announcements)
        {
            var heading = AnnouncementTexts.Heading(announcement);
            var paragraphs = Paragraphs(announcement);
            var lines = paragraphs.Sum(Lines);

            // Same heading and room left: share the slide. Otherwise a new slide; an
            // announcement too long for one slide carries on over the next, under its heading.
            if (current is null || currentHeading != heading || currentLines + lines > LinesPerSlide)
            {
                Start(heading);
            }
            foreach (var paragraph in paragraphs)
            {
                if (currentLines > 0 && currentLines + Lines(paragraph) > LinesPerSlide)
                {
                    Start(heading);
                }
                current!.Add(paragraph);
                currentLines += Lines(paragraph);
            }
        }

        foreach (var names in birthdayNames.Chunk(NamesPerSlide))
        {
            slides.Add(new DeckSlide(BirthdaysHeading, names.Select(n => new DeckParagraph(DeckParagraphKind.Name, null, n)).ToList()));
        }

        return new AnnouncementDeck(sunday, AnnouncementDate.Of(sunday), monthlyTheme, slides);
    }

    private static List<DeckParagraph> Paragraphs(Announcement announcement)
    {
        var paragraphs = new List<DeckParagraph>();
        if (AnnouncementTexts.WhenLine(announcement) is { } when)
        {
            paragraphs.Add(new DeckParagraph(DeckParagraphKind.When, null, when));
            paragraphs.Add(new DeckParagraph(DeckParagraphKind.Title, null, announcement.Title));
        }
        else
        {
            paragraphs.Add(new DeckParagraph(DeckParagraphKind.Headline, null, announcement.Title));
        }
        paragraphs.AddRange(announcement.Details.Select(d => IsNote(d.Label)
            ? new DeckParagraph(DeckParagraphKind.Note, d.Label, d.Text)
            : new DeckParagraph(DeckParagraphKind.Detail, d.Label, d.Text)));
        return paragraphs;
    }

    // "N.B." lines stand out (blue italic) rather than read as one more detail.
    private static bool IsNote(string? label) =>
        label is not null && label.Replace(".", "").Replace(" ", "").Equals("NB", StringComparison.OrdinalIgnoreCase);

    private static int Lines(DeckParagraph paragraph)
    {
        var length = paragraph.Text.Length + (paragraph.Label is null ? 0 : paragraph.Label.Length + 3);
        return Math.Max(1, (length + CharactersPerLine - 1) / CharactersPerLine);
    }
}

// The title slide's date, "Dimanche 1er novembre 2026", with "er" set apart: it is raised.
public sealed record AnnouncementDate(string Day, string DaySuffix, string Rest)
{
    public static AnnouncementDate Of(DateOnly sunday) =>
        new($"Dimanche {sunday.Day}", sunday.Day == 1 ? "er" : "", $" {AnnouncementTexts.MonthName(sunday.Month)} {sunday.Year}");

    public override string ToString() => $"{Day}{DaySuffix}{Rest}";
}

// One slide: its heading (the announcement's own, "Annonce externe", none...) and its lines.
public sealed record DeckSlide(string? Heading, IReadOnlyList<DeckParagraph> Paragraphs);

public enum DeckParagraphKind
{
    When,       // bold: "Samedi 31 octobre 2026, à 18h00 :"
    Title,      // red: what happens, under its when line
    Headline,   // red: what happens, opening an announcement that has no when line
    Detail,     // "Sujet : ..." with its label underlined
    Note,       // "N.B. : ..." in blue italic
    Name,       // a birthday
}

public sealed record DeckParagraph(DeckParagraphKind Kind, string? Label, string Text);
