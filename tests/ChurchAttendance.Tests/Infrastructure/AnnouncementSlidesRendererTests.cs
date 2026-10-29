using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Announcements;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Validation;
using Microsoft.Extensions.DependencyInjection;
using A = DocumentFormat.OpenXml.Drawing;

namespace ChurchAttendance.Tests.Infrastructure;

// The projected deck, opened back with the Open XML SDK: the church's 4:3 template, a title
// slide with the banner, then the announcements in their colours.
public class AnnouncementSlidesRendererTests
{
    private static readonly AnnouncementDeck Deck = AnnouncementDeck.Build(
        new DateOnly(2026, 11, 1),
        [
            new Announcement
            {
                Kind = AnnouncementKind.Event, EventDate = new DateOnly(2026, 12, 5), EventTime = "de 8h30 à 14h00",
                Heading = "2e jeûne annuel", Title = "Jeûne de l'église",
                Details = [new() { Label = "Sujet", Text = "2 Cor. 4 : 16" }, new() { Label = "N.B.", Text = "Soyez des nôtres !" }],
            },
        ],
        "Novembre : Mois de la famille",
        ["Kyan Mybensly Paul", "Claudette Silyon"]);

    private static byte[] Render()
    {
        using var factory = new CustomWebApplicationFactory();
        return factory.Services.GetRequiredService<IAnnouncementSlidesRenderer>().Render(Deck);
    }

    private static List<A.Run> Runs(SlidePart slide) => slide.Slide.Descendants<A.Run>().ToList();

    private static A.Run RunOf(SlidePart slide, string text) => Runs(slide).Single(r => r.Text!.Text == text);

    private static string? Color(A.Run run) => run.RunProperties!.GetFirstChild<A.SolidFill>()?.RgbColorModelHex?.Val?.Value;

    [Fact]
    public void Deck_IsAValid4By3Presentation_OnTheChurchTemplate()
    {
        using var document = PresentationDocument.Open(new MemoryStream(Render()), isEditable: false);

        var errors = new OpenXmlValidator().Validate(document).Select(e => $"{e.Path?.XPath}: {e.Description}").ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        var size = document.PresentationPart!.Presentation.SlideSize!;
        Assert.Equal((9144000, 6858000), (size.Cx!.Value, size.Cy!.Value));
        // Title slide, the announcement, the birthdays.
        Assert.Equal(3, document.PresentationPart.SlideParts.Count());
    }

    [Fact]
    public void TitleSlide_HasTheBanner_AndTheSundayWithA1erRaised()
    {
        using var document = PresentationDocument.Open(new MemoryStream(Render()), isEditable: false);
        var slides = SlidesInOrder(document);
        var title = slides[0];

        Assert.Equal(SlideLayoutValues.Title, title.SlideLayoutPart!.SlideLayout.Type!.Value);
        Assert.Equal("image/jpeg", Assert.Single(title.ImageParts).ContentType);
        Assert.Contains(Runs(title), r => r.Text!.Text == AnnouncementDeck.Title && r.RunProperties!.Underline! == A.TextUnderlineValues.Single);
        Assert.Equal(30000, RunOf(title, "er").RunProperties!.Baseline!.Value);
        Assert.Equal("Dimanche 1er novembre 2026", string.Concat(title.Slide.Descendants<A.Paragraph>().Last().Descendants<A.Text>().Select(t => t.Text)));
    }

    [Fact]
    public void AnnouncementSlide_UsesTheChurchsColours()
    {
        using var document = PresentationDocument.Open(new MemoryStream(Render()), isEditable: false);
        var slide = SlidesInOrder(document)[1];

        Assert.Equal(SlideLayoutValues.Object, slide.SlideLayoutPart!.SlideLayout.Type!.Value); // "Titre et contenu"
        var heading = RunOf(slide, "2e jeûne annuel");
        Assert.Equal(("FF0000", true), (Color(heading), heading.RunProperties!.Underline! == A.TextUnderlineValues.Single));
        Assert.Null(Color(RunOf(slide, "Samedi 5 décembre 2026, de 8h30 à 14h00 :")));
        Assert.Equal("FF0000", Color(RunOf(slide, "Jeûne de l'église")));
        Assert.True(RunOf(slide, "Sujet :").RunProperties!.Underline! == A.TextUnderlineValues.Single);
        Assert.Equal("00B0F0", Color(RunOf(slide, "2 Cor. 4 : 16")));
        var note = RunOf(slide, "Soyez des nôtres !");
        Assert.Equal(("00B0F0", true), (Color(note), note.RunProperties!.Italic!.Value));
        Assert.All(Runs(slide), r => Assert.True(r.RunProperties!.Bold!.Value));
    }

    [Fact]
    public void BirthdaySlide_ListsTheNames()
    {
        using var document = PresentationDocument.Open(new MemoryStream(Render()), isEditable: false);
        var slide = SlidesInOrder(document)[2];

        Assert.Equal([AnnouncementDeck.BirthdaysHeading, "Kyan Mybensly Paul", "Claudette Silyon"], Runs(slide).Select(r => r.Text!.Text));
    }

    private static List<SlidePart> SlidesInOrder(PresentationDocument document)
    {
        var presentation = document.PresentationPart!;
        return presentation.Presentation.SlideIdList!.Elements<SlideId>()
            .Select(id => (SlidePart)presentation.GetPartById(id.RelationshipId!.Value!))
            .ToList();
    }
}
