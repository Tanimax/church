using ChurchAttendance.Application.Announcements;

namespace ChurchAttendance.Tests.Common;

// The bold first line and the heading of an announcement, written as on the church's slides,
// and the rule that takes a dated announcement out once its date has passed.
public class AnnouncementTextsTests
{
    private static Announcement Make(AnnouncementKind kind = AnnouncementKind.Event, DateOnly? date = null,
        DateOnly? end = null, string? time = null, string? schedule = null, string? heading = null) =>
        new()
        {
            Kind = kind, EventDate = date, EndDate = end, EventTime = time, Schedule = schedule, Heading = heading,
            Title = "Conférence du ministère des hommes et celui des dames.",
        };

    [Theory]
    [InlineData(2026, 10, 31, null, null, null, "à 18h00", "Samedi 31 octobre 2026, à 18h00 :")]
    [InlineData(2026, 12, 5, null, null, null, "de 8h30 à 14h00", "Samedi 5 décembre 2026, de 8h30 à 14h00 :")]
    [InlineData(2026, 11, 1, null, null, null, null, "Dimanche 1er novembre 2026 :")]
    [InlineData(2026, 11, 6, 2026, 11, 8, null, "Du vendredi 6 au dimanche 8 novembre 2026 :")]
    [InlineData(2026, 10, 30, 2026, 11, 1, null, "Du vendredi 30 octobre au dimanche 1er novembre 2026 :")]
    [InlineData(2026, 12, 31, 2027, 1, 2, null, "Du jeudi 31 décembre 2026 au samedi 2 janvier 2027 :")]
    public void WhenLine_OfADatedAnnouncement_SpellsTheDatesOut(int y, int m, int d, int? ey, int? em, int? ed, string? time, string expected)
    {
        var end = ey is null ? (DateOnly?)null : new DateOnly(ey.Value, em!.Value, ed!.Value);

        Assert.Equal(expected, AnnouncementTexts.WhenLine(Make(date: new DateOnly(y, m, d), end: end, time: time)));
    }

    [Fact]
    public void WhenLine_OfARecurringActivity_IsItsSchedule()
    {
        var announcement = Make(AnnouncementKind.Recurring, schedule: "Mardi et vendredi de 12h00 à 12h30");

        Assert.Equal("Mardi et vendredi de 12h00 à 12h30 :", AnnouncementTexts.WhenLine(announcement));
    }

    [Fact]
    public void WhenLine_WithNeitherDateNorSchedule_IsNone()
    {
        Assert.Null(AnnouncementTexts.WhenLine(Make(AnnouncementKind.Pastoral)));
    }

    [Theory]
    [InlineData(AnnouncementKind.Event, null, "Octobre : Mois d'actions de grâces")]
    [InlineData(AnnouncementKind.Event, "2e jeûne annuel", "2e jeûne annuel")]
    [InlineData(AnnouncementKind.External, null, "Annonce externe")]
    [InlineData(AnnouncementKind.External, "Réveil des dames", "Réveil des dames")]
    public void Heading_IsItsOwn_ElseAnnonceExterne_ElseTheMonthsTheme(AnnouncementKind kind, string? heading, string expected)
    {
        Assert.Equal(expected, AnnouncementTexts.Heading(Make(kind, heading: heading), "Octobre : Mois d'actions de grâces"));
    }

    [Theory]
    [InlineData(2026, 10, 25, true)]   // the Sunday before the event
    [InlineData(2026, 10, 31, true)]   // the day itself (were it a Sunday)
    [InlineData(2026, 11, 1, false)]   // the Sunday after: over
    public void IsShownOn_UntilItsDate(int y, int m, int d, bool shown)
    {
        Assert.Equal(shown, Make(date: new DateOnly(2026, 10, 31)).IsShownOn(new DateOnly(y, m, d)));
    }

    [Fact]
    public void IsShownOn_ForSeveralDays_UntilTheLastOne()
    {
        var revival = Make(AnnouncementKind.External, new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 8));

        Assert.True(revival.IsShownOn(new DateOnly(2026, 11, 8)));
        Assert.False(revival.IsShownOn(new DateOnly(2026, 11, 15)));
    }

    [Fact]
    public void IsShownOn_StandingAnnouncements_Always_UnlessArchived()
    {
        var choir = Make(AnnouncementKind.Recurring, schedule: "Vendredi soir de 19h00 à 21h30");

        Assert.True(choir.IsShownOn(new DateOnly(2030, 1, 6)));
        choir.IsArchived = true;
        Assert.False(choir.IsShownOn(new DateOnly(2026, 10, 25)));
    }
}
