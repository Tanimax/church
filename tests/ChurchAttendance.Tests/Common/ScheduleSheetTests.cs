using ChurchAttendance.Application.Schedules;

namespace ChurchAttendance.Tests.Common;

public class ScheduleSheetTests
{
    private static ScheduleFormModel Q4(string? footer = "Si la date ne vous convient pas, communiquer avec la sœur Nativita.") => new()
    {
        Year = 2026,
        Quarter = 4,
        Exists = true,
        FooterNote = footer,
        Rows = ServiceSchedule.SundaysOf(2026, 4).Select(d => new ScheduleRow { Date = d }).ToList(),
    };

    [Fact]
    public void From_WordsTheSheetLikeTheOriginal()
    {
        var quarter = Q4();
        quarter.Rows[4].Leader = "Sr Roselore Blaise";       // 1er novembre
        quarter.Rows[4].Preacher = "Pasteur Gilbert Baptiste";

        var sheet = ScheduleSheet.From(quarter);

        Assert.Equal("Service dominical", sheet.Title);
        Assert.Equal("Du 4 octobre au 27 décembre 2026", sheet.Period);
        Assert.Equal(13, sheet.Rows.Count);
        Assert.Equal(new ScheduleSheetRow("4", "", "oct.", 0, "", ""), sheet.Rows[0]);
        Assert.Equal(new ScheduleSheetRow("1", "er", "nov.", 1, "Sr Roselore Blaise", "Pasteur Gilbert Baptiste"), sheet.Rows[4]);
        Assert.Equal(2, sheet.Rows[^1].MonthInQuarter);
        Assert.Equal("N.B. Si la date ne vous convient pas, communiquer avec la sœur Nativita.", sheet.FooterNote);
    }

    [Theory]
    [InlineData("N.B. Déjà préfixée.", "N.B. Déjà préfixée.")]
    [InlineData("  n.b. en minuscules ", "n.b. en minuscules")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void From_PrefixesTheNoteWithNB_Once(string? note, string? expected)
    {
        Assert.Equal(expected, ScheduleSheet.From(Q4(note)).FooterNote);
    }
}
