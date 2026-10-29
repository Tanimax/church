using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Schedules;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;

namespace ChurchAttendance.Tests.Infrastructure;

// Reads the generated PDF back, as a viewer would: one Letter page, the sheet's text, its
// font embedded (the Docker image has no fonts) and the banner.
public class SchedulePdfRendererTests
{
    private static ScheduleSheet Sheet(int year, int quarter, Func<int, string> leader, Func<int, string> preacher)
    {
        var rows = ServiceSchedule.SundaysOf(year, quarter)
            .Select((d, i) => new ScheduleRow { Date = d, Leader = leader(i), Preacher = preacher(i) })
            .ToList();
        return ScheduleSheet.From(new ScheduleFormModel
        {
            Year = year, Quarter = quarter, Exists = true, Rows = rows,
            FooterNote = "Si la date ne vous convient pas, communiquer avec la sœur Nativita Cherival au 514-442-8652",
        });
    }

    private static byte[] Render(ScheduleSheet sheet)
    {
        using var factory = new CustomWebApplicationFactory();
        return factory.Services.GetRequiredService<ISchedulePdfRenderer>().Render(sheet);
    }

    [Fact]
    public void Render_Q4_2026_IsOneLetterPage_WithTheSheetsText()
    {
        var bytes = Render(Sheet(2026, 4, i => $"Sr Dirigeante {i}", i => i == 2 ? "Pasteur Gabriel D. Richemond" : $"Pasteur {i}"));

        using var pdf = PdfDocument.Open(bytes);
        var page = Assert.Single(pdf.GetPages());
        Assert.Equal(612, page.Width, 0);
        Assert.Equal(792, page.Height, 0);
        var text = string.Join(" ", page.GetWords().Select(w => w.Text));
        Assert.Contains("Service dominical", text);
        Assert.Contains("Du 4 octobre au 27 décembre 2026", text);
        Assert.Contains("Dirigeant (e)", text);
        Assert.Contains("Prédicateur (trice)", text);
        Assert.Contains("Pasteur Gabriel D. Richemond", text);
        Assert.Contains("Sr Dirigeante 12", text);
        Assert.Contains("27 déc.", text);
        Assert.Contains("N.B. Si la date ne vous convient pas", text);
        Assert.Contains(page.Letters, l => l.FontName.Contains("Liberation Sans", StringComparison.Ordinal));
        Assert.Single(page.GetImages()); // the banner
    }

    [Fact]
    public void Render_AFourteenSundayQuarter_StillFitsOnOnePage()
    {
        var bytes = Render(Sheet(2029, 3, _ => "Sr Nelfie Alcé Michel", _ => "Pasteur Gabriel D. Richemond"));

        using var pdf = PdfDocument.Open(bytes);
        Assert.Equal(1, pdf.NumberOfPages);
    }

    [Fact]
    public void Render_EmptyQuarter_IsAValidSheet()
    {
        var bytes = Render(Sheet(2027, 1, _ => "", _ => ""));

        using var pdf = PdfDocument.Open(bytes);
        Assert.Contains("Du 3 janvier au 28 mars 2027", string.Join(" ", pdf.GetPage(1).GetWords().Select(w => w.Text)));
    }
}
