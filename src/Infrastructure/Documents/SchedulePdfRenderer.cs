using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Schedules;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Microsoft.AspNetCore.Hosting;

namespace ChurchAttendance.Infrastructure.Documents;

// The "Service dominical" sheet, reproduced from the church's own (LibreOffice) version:
// Letter page, banner, bold underlined title and period, a boxed three-column table whose rows
// take the colour of their month, then the N.B. line.
internal sealed class SchedulePdfRenderer(IWebHostEnvironment env) : ISchedulePdfRenderer
{
    // The sheet's colours for the 1st, 2nd and 3rd month of the quarter (measured on it).
    private static readonly Color[] MonthColors =
        [Color.FromRgb(0x00, 0xB0, 0x50), Color.FromRgb(0xD8, 0x6D, 0xCB), Color.FromRgb(0x0F, 0x9E, 0xD5)];

    // Measured on the original sheet: text from 1.25" on the left, table 6.05" wide.
    private static readonly Unit ContentWidth = Unit.FromInch(6.05);

    public byte[] Render(ScheduleSheet sheet)
    {
        DocumentAssets.EnsureRegistered();

        var document = new Document();
        document.Info.Title = $"{sheet.Title} — {sheet.Period}";
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = DocumentAssets.FamilyName;
        normal.Font.Size = 14;

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.Letter;
        section.PageSetup.TopMargin = Unit.FromInch(0.7);
        section.PageSetup.BottomMargin = Unit.FromInch(0.7);
        section.PageSetup.LeftMargin = Unit.FromInch(1.25);
        section.PageSetup.RightMargin = Unit.FromInch(8.5 - 1.25) - ContentWidth;

        var banner = section.AddImage("base64:" + Convert.ToBase64String(Branding.ReadBanner(env.WebRootFileProvider)));
        banner.Width = ContentWidth;
        banner.LockAspectRatio = true;

        AddHeading(section, sheet.Title, spaceBefore: 6);
        AddHeading(section, sheet.Period, spaceBefore: 0);

        var table = section.AddTable();
        table.Format.SpaceBefore = Unit.FromPoint(4);
        table.Format.SpaceAfter = Unit.FromPoint(4);
        table.AddColumn(Unit.FromInch(0.8));
        table.AddColumn(Unit.FromInch(2.35));
        table.AddColumn(ContentWidth - Unit.FromInch(3.15)); // "Pasteur Gabriel D. Richemond" on one line
        table.TopPadding = Unit.FromPoint(9);
        table.BottomPadding = Unit.FromPoint(1);

        var header = table.AddRow();
        header.Format.Font.Bold = true;
        header.Format.Font.Size = 13;
        header.TopPadding = Unit.FromPoint(4);
        header.Cells[0].AddParagraph("Date");
        header.Cells[1].AddParagraph("Dirigeant (e)");
        header.Cells[2].AddParagraph("Prédicateur (trice)");

        foreach (var sunday in sheet.Rows)
        {
            var row = table.AddRow();
            row.Format.Font.Color = MonthColors[sunday.MonthInQuarter];
            var date = row.Cells[0].AddParagraph(sunday.Day);
            if (sunday.DaySuffix.Length > 0)
            {
                date.AddFormattedText(sunday.DaySuffix).Superscript = true;
            }
            date.AddText(" " + sunday.Month);
            row.Cells[1].AddParagraph(sunday.Leader);
            row.Cells[2].AddParagraph(sunday.Preacher);
        }
        table.Rows[^1].BottomPadding = Unit.FromPoint(9);
        table.SetEdge(0, 0, 3, table.Rows.Count, Edge.Box, BorderStyle.Single, Unit.FromPoint(0.75), Colors.Black);

        if (sheet.FooterNote is not null)
        {
            section.AddParagraph(sheet.FooterNote);
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static void AddHeading(Section section, string text, int spaceBefore)
    {
        var heading = section.AddParagraph();
        heading.Format.Alignment = ParagraphAlignment.Center;
        heading.Format.SpaceBefore = Unit.FromPoint(spaceBefore);
        heading.Format.Font.Size = 16;
        heading.Format.Font.Bold = true;
        heading.Format.Font.Underline = Underline.Single;
        heading.AddText(text);
    }
}
