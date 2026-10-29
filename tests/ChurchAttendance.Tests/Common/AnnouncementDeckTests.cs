using ChurchAttendance.Application.Announcements;

namespace ChurchAttendance.Tests.Common;

// How Sunday's announcements are spread over the projected slides.
public class AnnouncementDeckTests
{
    private const string Theme = "Novembre : Mois de la famille";
    private static readonly DateOnly Sunday = new(2026, 11, 1);

    private static Announcement Recurring(string title, string schedule = "Mercredi soir de 19h00 à 21h15") =>
        new() { Kind = AnnouncementKind.Recurring, Schedule = schedule, Title = title };

    private static AnnouncementDeck Build(params Announcement[] announcements) =>
        AnnouncementDeck.Build(Sunday, announcements, Theme, []);

    [Fact]
    public void Announcement_ReadsWhenThenTitleThenDetails()
    {
        var fast = new Announcement
        {
            Kind = AnnouncementKind.Event, EventDate = new DateOnly(2026, 12, 5), EventTime = "de 8h30 à 14h00",
            Heading = "2e jeûne annuel", Title = "Jeûne de l'église",
            Details = [new() { Label = "Sujet", Text = "2 Cor. 4 : 16" }, new() { Label = "N.B.", Text = "Soyez des nôtres !" }],
        };

        var slide = Assert.Single(Build(fast).Slides);

        Assert.Equal("2e jeûne annuel", slide.Heading);
        Assert.Equal(
            [
                new DeckParagraph(DeckParagraphKind.When, null, "Samedi 5 décembre 2026, de 8h30 à 14h00 :"),
                new DeckParagraph(DeckParagraphKind.Title, null, "Jeûne de l'église"),
                new DeckParagraph(DeckParagraphKind.Detail, "Sujet", "2 Cor. 4 : 16"),
                new DeckParagraph(DeckParagraphKind.Note, "N.B.", "Soyez des nôtres !"),
            ],
            slide.Paragraphs);
    }

    [Fact]
    public void Announcement_WithoutWhen_OpensWithItsTitle()
    {
        var sick = new Announcement { Kind = AnnouncementKind.Pastoral, Title = "Les gens malades…" };

        Assert.Equal(DeckParagraphKind.Headline, Build(sick).Slides[0].Paragraphs[0].Kind);
    }

    [Fact]
    public void Announcements_UnderTheSameHeading_ShareASlide_WhileTheyFit()
    {
        var deck = Build(Recurring("Réunion de prière"), Recurring("Répétition de la chorale"),
            new Announcement { Kind = AnnouncementKind.External, EventDate = new DateOnly(2026, 11, 6), Title = "Réveil des dames" });

        Assert.Equal([Theme, AnnouncementTexts.ExternalHeading], deck.Slides.Select(s => s.Heading));
        Assert.Equal(4, deck.Slides[0].Paragraphs.Count);
    }

    [Fact]
    public void Announcements_ThatNoLongerFit_GoOnTheNextSlide_UnderTheSameHeading()
    {
        // Two lines each (when + title): four announcements fill a slide.
        var deck = Build(Enumerable.Range(1, 5).Select(i => Recurring($"Activité {i}")).ToArray());

        Assert.Equal(AnnouncementDeck.LinesPerSlide / 2 * 2, deck.Slides[0].Paragraphs.Count);
        Assert.Equal([Theme, Theme], deck.Slides.Select(s => s.Heading));
        Assert.Equal("Activité 5", deck.Slides[1].Paragraphs[1].Text);
    }

    [Fact]
    public void LongAnnouncement_CarriesOnOverTheNextSlide()
    {
        var revival = new Announcement
        {
            Kind = AnnouncementKind.External, EventDate = new DateOnly(2026, 11, 6), EndDate = new DateOnly(2026, 11, 8),
            Title = "L'Église Par la foi de l'ACM, dirigée par pasteur Ernst Civil, nous invite au réveil des dames.",
            Details =
            [
                new() { Label = "Sujet", Text = "Jean 14 : 1-3 — Préparons-nous pour le grand rendez-vous avec le Seigneur." },
                new() { Label = "Prédicateur", Text = "Révérend docteur Darson St-Fort" },
                new() { Label = "Horaire", Text = "Vendredi et samedi : 19h00 – 21h30, dimanche : 18h00 – 21h00" },
            ],
        };

        var deck = Build(revival);

        Assert.Equal(2, deck.Slides.Count);
        Assert.All(deck.Slides, s => Assert.Equal(AnnouncementTexts.ExternalHeading, s.Heading));
        Assert.Equal(5, deck.Slides.Sum(s => s.Paragraphs.Count)); // when, title, 3 details: nothing lost on the way
    }

    [Fact]
    public void Birthdays_EndTheDeck_SevenNamesPerSlide()
    {
        var names = Enumerable.Range(1, 9).Select(i => $"Membre {i}").ToList();

        var deck = AnnouncementDeck.Build(Sunday, [Recurring("Réunion de prière")], Theme, names);

        Assert.Equal([Theme, AnnouncementDeck.BirthdaysHeading, AnnouncementDeck.BirthdaysHeading], deck.Slides.Select(s => s.Heading));
        Assert.Equal(names, deck.Slides.Skip(1).SelectMany(s => s.Paragraphs).Select(p => p.Text));
        Assert.All(deck.Slides.Skip(1).SelectMany(s => s.Paragraphs), p => Assert.Equal(DeckParagraphKind.Name, p.Kind));
        Assert.Equal(AnnouncementDeck.NamesPerSlide, deck.Slides[1].Paragraphs.Count);
    }

    [Fact]
    public void NoBirthday_NoBirthdaySlide()
    {
        Assert.DoesNotContain(Build(Recurring("Réunion de prière")).Slides, s => s.Heading == AnnouncementDeck.BirthdaysHeading);
    }

    [Theory]
    [InlineData(2026, 11, 1, "Dimanche 1", "er", " novembre 2026")]
    [InlineData(2026, 10, 25, "Dimanche 25", "", " octobre 2026")]
    public void Date_OfTheTitleSlide_RaisesThe1er(int y, int m, int d, string day, string suffix, string rest)
    {
        Assert.Equal(new AnnouncementDate(day, suffix, rest), AnnouncementDate.Of(new DateOnly(y, m, d)));
        Assert.Equal($"{day}{suffix}{rest}", AnnouncementDate.Of(new DateOnly(y, m, d)).ToString());
    }
}
