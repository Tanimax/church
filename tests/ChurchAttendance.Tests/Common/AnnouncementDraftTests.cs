using ChurchAttendance.Application.Announcements;

namespace ChurchAttendance.Tests.Common;

// The simplified announcement form ("when?", "another church's?", details as lines) maps to
// the same announcement the four-kind form produced.
public class AnnouncementDraftTests
{
    [Theory]
    [InlineData(true, false, null, AnnouncementKind.Event)]
    [InlineData(false, false, "Mercredi soir de 19h00 à 21h15", AnnouncementKind.Recurring)]
    [InlineData(false, false, "  ", AnnouncementKind.Pastoral)]
    [InlineData(true, true, null, AnnouncementKind.External)]
    [InlineData(false, true, null, AnnouncementKind.External)]
    public void Kind_IsWorkedOutFromTheAnswers(bool dated, bool external, string? schedule, AnnouncementKind expected)
    {
        Assert.Equal(expected, new AnnouncementDraft { IsDated = dated, IsExternal = external, Schedule = schedule }.Kind);
    }

    [Fact]
    public void ToForm_KeepsOnlyTheWhenOfTheChosenAnswer()
    {
        var draft = new AnnouncementDraft
        {
            Title = "Soirée", Date = new DateOnly(2026, 11, 21), Time = "à 18h00", Schedule = "Mercredi soir",
        };

        var dated = draft.ToForm();
        draft.IsDated = false;
        var weekly = draft.ToForm();

        Assert.Equal((new DateOnly(2026, 11, 21), "à 18h00", (string?)null), (dated.EventDate, dated.EventTime, dated.Schedule));
        Assert.Equal(((DateOnly?)null, (string?)null, "Mercredi soir"), (weekly.EventDate, weekly.EventTime, weekly.Schedule));
    }

    [Fact]
    public void Details_OneLineEach_WithTheirLabelWhenTheyStartWithOne()
    {
        var details = AnnouncementDraft.ParseDetails(
            "Sujet : 2 Cor. 4 : 16\r\n\n  N.B. : Soyez des nôtres !  \n2 Cor. 4 : 16 seul\nTél. : 438-526-1649\nSans étiquette");

        Assert.Equal(
            [("Sujet", "2 Cor. 4 : 16"), ("N.B.", "Soyez des nôtres !"), (null, "2 Cor. 4 : 16 seul"), ("Tél.", "438-526-1649"), (null, "Sans étiquette")],
            details.Select(d => (d.Label, d.Text)));
    }

    [Fact]
    public void From_ThenToForm_GivesTheSameAnnouncementBack()
    {
        var revival = new Announcement
        {
            Id = 7, Kind = AnnouncementKind.External, EventDate = new DateOnly(2026, 11, 6), EndDate = new DateOnly(2026, 11, 8),
            Heading = "Réveil", Title = "L'Église Par la foi nous invite au réveil des dames.",
            Details = [new() { Label = "Sujet", Text = "Jean 14 : 1-3" }, new() { Text = "Bienvenue à tous" }],
        };

        var form = AnnouncementDraft.From(revival).ToForm();

        Assert.Equal((7, AnnouncementKind.External, "Réveil", revival.Title), (form.Id, form.Kind, form.Heading, form.Title));
        Assert.Equal((revival.EventDate, revival.EndDate), (form.EventDate, form.EndDate));
        Assert.Equal([("Sujet", "Jean 14 : 1-3"), (null, "Bienvenue à tous")], form.Details.Select(d => (d.Label, d.Text)));
    }

    [Fact]
    public void From_AWeeklyActivity_IsNotDated()
    {
        var choir = new Announcement { Kind = AnnouncementKind.Recurring, Schedule = "Vendredi soir", Title = "Chorale" };

        var draft = AnnouncementDraft.From(choir);

        Assert.False(draft.IsDated);
        Assert.Equal(AnnouncementKind.Recurring, draft.Kind);
    }
}
