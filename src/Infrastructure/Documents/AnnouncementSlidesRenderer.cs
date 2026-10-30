using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Announcements;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.AspNetCore.Hosting;
using A = DocumentFormat.OpenXml.Drawing;

namespace ChurchAttendance.Infrastructure.Documents;

// Sunday's announcements as a PowerPoint deck, on the church's own template
// (Documents/Assets/annonces-modele.pptx: the secretariat's deck with its slides removed, so
// its 4:3 size, Calibri theme and layouts stay). Like their slides: a title slide with the
// banner, the date and the month's theme, then "title and content" slides in 36-point bold,
// the heading (when the announcement has one) red and underlined,
// what happens in red, detail labels underlined and their text blue, N.B. in blue italic.
internal sealed class AnnouncementSlidesRenderer(IWebHostEnvironment env) : IAnnouncementSlidesRenderer
{
    private const string Template = "annonces-modele.pptx";
    private const string Red = "FF0000";
    private const string Blue = "00B0F0";
    private const int TextSize = 3600; // hundredths of a point
    private const int NameSize = 4000;
    private const int ThemeSize = 3200;

    // The banner sits where the template's own one did, across the top of the title slide.
    private const long BannerLeft = 611560;
    private const long BannerTop = 188640;
    private const long BannerWidth = 7391400;

    // Indent of the text after a bullet (the template's level-1 marL): lines without a bullet
    // start there, under the text of the line above.
    private const int TextIndent = 342900;

    public byte[] Render(AnnouncementDeck deck)
    {
        using var stream = new MemoryStream();
        stream.Write(DocumentAssets.Resource(Template));
        stream.Position = 0;

        using (var document = PresentationDocument.Open(stream, isEditable: true))
        {
            var presentationPart = document.PresentationPart!;
            var layouts = presentationPart.SlideMasterParts.Single().SlideLayoutParts.ToList();
            var titleLayout = layouts.Single(l => l.SlideLayout.Type?.Value == SlideLayoutValues.Title);
            var contentLayout = layouts.Single(l => l.SlideLayout.Type?.Value == SlideLayoutValues.Object);
            var slideIds = presentationPart.Presentation.SlideIdList!;

            void Add(SlidePart slidePart) => slideIds.Append(new SlideId
            {
                Id = (uint)(256 + slideIds.ChildElements.Count),
                RelationshipId = presentationPart.GetIdOfPart(slidePart),
            });

            Add(TitleSlide(presentationPart, titleLayout, deck.Date, deck.MonthlyTheme));
            foreach (var slide in deck.Slides)
            {
                Add(ContentSlide(presentationPart, contentLayout, slide));
            }
            presentationPart.Presentation.Save();
        }
        return stream.ToArray();
    }

    private SlidePart TitleSlide(PresentationPart presentation, SlideLayoutPart layout, AnnouncementDate date, string? theme)
    {
        var slidePart = NewSlide(presentation, layout);
        var tree = slidePart.Slide.CommonSlideData!.ShapeTree!;

        tree.Append(Placeholder(2, "Titre", new PlaceholderShape { Type = PlaceholderValues.CenteredTitle },
            new A.Paragraph(Run(AnnouncementDeck.Title, italic: true, underline: true))));
        var dateParagraph = new A.Paragraph(Run(date.Day, italic: true, schemeText: true));
        if (date.DaySuffix.Length > 0)
        {
            dateParagraph.Append(Run(date.DaySuffix, italic: true, schemeText: true, raised: true));
        }
        dateParagraph.Append(Run(date.Rest, italic: true, schemeText: true));
        var subtitle = new List<A.Paragraph> { dateParagraph };
        if (theme is not null)
        {
            // Said once here, under the date, rather than over every announcement.
            subtitle.Add(new A.Paragraph(Run(theme, italic: true, underline: true, color: Red, size: ThemeSize)));
        }
        tree.Append(Placeholder(3, "Date", new PlaceholderShape { Type = PlaceholderValues.SubTitle, Index = 1 }, subtitle.ToArray()));

        var banner = Branding.ReadBanner(env.WebRootFileProvider);
        var image = slidePart.AddImagePart(ImagePartType.Jpeg);
        using (var bannerStream = new MemoryStream(banner))
        {
            image.FeedData(bannerStream);
        }
        var size = SixLabors.ImageSharp.Image.Identify(banner).Size;
        var height = BannerWidth * size.Height / size.Width;
        tree.Append(new Picture(
            new NonVisualPictureProperties(
                new NonVisualDrawingProperties { Id = 4, Name = "Bandeau", Description = "Église De Dieu De Béthel" },
                new NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                new ApplicationNonVisualDrawingProperties()),
            new BlipFill(new A.Blip { Embed = slidePart.GetIdOfPart(image) }, new A.Stretch(new A.FillRectangle())),
            new ShapeProperties(
                new A.Transform2D(new A.Offset { X = BannerLeft, Y = BannerTop }, new A.Extents { Cx = BannerWidth, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })));
        return slidePart;
    }

    private static SlidePart ContentSlide(PresentationPart presentation, SlideLayoutPart layout, DeckSlide slide)
    {
        var slidePart = NewSlide(presentation, layout);
        var tree = slidePart.Slide.CommonSlideData!.ShapeTree!;

        // Most announcements have no heading of their own: no title shape at all then, rather
        // than an empty one PowerPoint shows as "Cliquez pour ajouter un titre" when editing.
        if (slide.Heading is not null)
        {
            tree.Append(Placeholder(2, "Titre", new PlaceholderShape { Type = PlaceholderValues.Title },
                new A.Paragraph(Run(slide.Heading, italic: true, underline: true, color: Red))));
        }
        tree.Append(Placeholder(3, "Annonces", new PlaceholderShape { Index = 1 }, slide.Paragraphs.Select(Paragraph).ToArray()));
        return slidePart;
    }

    private static A.Paragraph Paragraph(DeckParagraph paragraph) => paragraph.Kind switch
    {
        DeckParagraphKind.When => new A.Paragraph(Run(paragraph.Text)),
        DeckParagraphKind.Headline => new A.Paragraph(Run(paragraph.Text, color: Red)),
        DeckParagraphKind.Title => new A.Paragraph(Unbulleted(), Run(paragraph.Text, color: Red)),
        DeckParagraphKind.Detail => Labelled(new A.Paragraph(), paragraph, italic: false),
        DeckParagraphKind.Note => Labelled(new A.Paragraph(Unbulleted()), paragraph, italic: true),
        DeckParagraphKind.Name => new A.Paragraph(Run(paragraph.Text, size: NameSize)),
        _ => throw new ArgumentOutOfRangeException(nameof(paragraph), paragraph.Kind, null),
    };

    // "Sujet :" underlined, then the text in blue.
    private static A.Paragraph Labelled(A.Paragraph line, DeckParagraph paragraph, bool italic)
    {
        if (paragraph.Label is not null)
        {
            line.Append(Run($"{paragraph.Label} :", italic: italic, underline: true));
            line.Append(Run(" ", italic: italic));
        }
        line.Append(Run(paragraph.Text, italic: italic, color: Blue));
        return line;
    }

    // A line carrying on the announcement above it: no bullet, aligned on that text.
    private static A.ParagraphProperties Unbulleted() =>
        new(new A.NoBullet()) { LeftMargin = TextIndent, Indent = 0 };

    private static SlidePart NewSlide(PresentationPart presentation, SlideLayoutPart layout)
    {
        var slidePart = presentation.AddNewPart<SlidePart>();
        slidePart.Slide = new Slide(
            new CommonSlideData(new ShapeTree(
                new NonVisualGroupShapeProperties(
                    new NonVisualDrawingProperties { Id = 1, Name = "" },
                    new NonVisualGroupShapeDrawingProperties(),
                    new ApplicationNonVisualDrawingProperties()),
                new GroupShapeProperties(new A.TransformGroup()))),
            new ColorMapOverride(new A.MasterColorMapping()));
        slidePart.AddPart(layout);
        return slidePart;
    }

    // A shape filling one of the layout's placeholders: its position and bullets come from the
    // template. Text that still overflows is shrunk by PowerPoint rather than cut.
    private static Shape Placeholder(uint id, string name, PlaceholderShape placeholder, params A.Paragraph[] paragraphs) =>
        new(
            new NonVisualShapeProperties(
                new NonVisualDrawingProperties { Id = id, Name = name },
                new NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties(placeholder)),
            new ShapeProperties(),
            new TextBody(new OpenXmlElement[] { new A.BodyProperties(new A.NormalAutoFit()), new A.ListStyle() }.Concat(paragraphs)));

    private static A.Run Run(string text, bool italic = false, bool underline = false, string? color = null,
        bool schemeText = false, bool raised = false, int size = TextSize)
    {
        var properties = new A.RunProperties { Language = "fr-CA", FontSize = size, Bold = true, Italic = italic };
        if (underline)
        {
            properties.Underline = A.TextUnderlineValues.Single;
        }
        if (raised)
        {
            properties.Baseline = 30000;
        }
        if (color is not null)
        {
            properties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = color }));
        }
        else if (schemeText)
        {
            properties.Append(new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Text1 }));
        }
        return new A.Run(properties, new A.Text(text));
    }
}
