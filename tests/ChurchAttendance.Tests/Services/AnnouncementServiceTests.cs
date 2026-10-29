using ChurchAttendance.Application.Announcements;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Tests.Services;

// The catalogue of announcements and the month's theme, as the Annonces page uses them.
public class AnnouncementServiceTests
{
    private static AnnouncementService Service(CustomWebApplicationFactory factory) =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<AnnouncementService>();

    private static AnnouncementFormModel Conference() => new()
    {
        Kind = AnnouncementKind.Event,
        EventDate = new DateOnly(2026, 10, 31),
        EventTime = " à 18h00 ",
        Title = " Conférence du ministère des hommes et celui des dames. ",
        Details =
        [
            new AnnouncementDetailRow { Label = " Sujet ", Text = " ‘’L’identité des ouvriers dans l’Église.’’ " },
            new AnnouncementDetailRow { Label = "N.B.", Text = "   " }, // a label alone: dropped
            new AnnouncementDetailRow { Text = "Soyez des nôtres !" },
        ],
    };

    [Fact]
    public async Task SaveAsync_NewEvent_IsSaved_Trimmed_WithoutEmptyLines()
    {
        using var factory = new CustomWebApplicationFactory();

        var result = await Service(factory).SaveAsync(Conference());

        Assert.True(result.IsSuccess);
        await using var db = factory.CreateDbContext();
        var saved = await db.Announcements.SingleAsync(a => a.Id == result.Value);
        Assert.Equal(AnnouncementKind.Event, saved.Kind);
        Assert.Equal("Conférence du ministère des hommes et celui des dames.", saved.Title);
        Assert.Equal("à 18h00", saved.EventTime);
        Assert.Equal([("Sujet", "‘’L’identité des ouvriers dans l’Église.’’"), (null, "Soyez des nôtres !")],
            saved.Details.Select(d => (d.Label, d.Text)));
        Assert.False(saved.IsArchived);
    }

    [Fact]
    public async Task SaveAsync_Existing_IsUpdated_AndSwitchingToRecurringDropsTheDate()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = Service(factory);
        var id = (await service.SaveAsync(Conference())).Value;

        var form = AnnouncementFormModel.From((await service.GetAllAsync(includeArchived: false)).Single());
        form.Kind = AnnouncementKind.Recurring;
        form.Schedule = "Vendredi soir de 19h00 à 21h30";
        form.Title = "Répétition de la chorale";
        form.Details.Clear();
        var result = await service.SaveAsync(form);

        Assert.Equal(id, result.Value);
        var saved = (await service.GetAllAsync(includeArchived: false)).Single();
        Assert.Equal(("Répétition de la chorale", "Vendredi soir de 19h00 à 21h30"), (saved.Title, saved.Schedule));
        Assert.Null(saved.EventDate);
        Assert.Null(saved.EventTime);
        Assert.Empty(saved.Details);
    }

    public static TheoryData<AnnouncementFormModel, string> Invalid() => new()
    {
        { new AnnouncementFormModel { Kind = AnnouncementKind.Event, EventDate = new DateOnly(2026, 10, 31), Title = "  " }, "announcement.title_required" },
        { new AnnouncementFormModel { Kind = AnnouncementKind.Recurring, Title = "Répétition de la chorale" }, "announcement.schedule_required" },
        { new AnnouncementFormModel { Kind = AnnouncementKind.Event, Title = "Soirée des dames" }, "announcement.event_date_required" },
        { new AnnouncementFormModel { Kind = AnnouncementKind.External, EndDate = new DateOnly(2026, 11, 8), Title = "Réveil" }, "announcement.end_date_without_start" },
        { new AnnouncementFormModel { Kind = AnnouncementKind.External, EventDate = new DateOnly(2026, 11, 8), EndDate = new DateOnly(2026, 11, 6), Title = "Réveil" }, "announcement.end_before_start" },
        { new AnnouncementFormModel { Kind = AnnouncementKind.Pastoral, Title = new string('x', AnnouncementService.MaxTitleLength + 1) }, "announcement.too_long" },
        { new AnnouncementFormModel { Kind = AnnouncementKind.Pastoral, Title = "Malades",
            Details = Enumerable.Range(0, AnnouncementService.MaxDetails + 1).Select(i => new AnnouncementDetailRow { Text = $"Ligne {i}" }).ToList() }, "announcement.too_many_details" },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public async Task SaveAsync_Invalid_IsRefused_AndNothingIsSaved(AnnouncementFormModel form, string code)
    {
        using var factory = new CustomWebApplicationFactory();

        var result = await Service(factory).SaveAsync(form);

        Assert.Equal(code, result.Error?.Code);
        await using var db = factory.CreateDbContext();
        Assert.Empty(db.Announcements);
    }

    [Fact]
    public async Task SaveAsync_UnknownAnnouncement_IsNotFound()
    {
        using var factory = new CustomWebApplicationFactory();
        var form = Conference();
        form.Id = 999_999;

        Assert.Equal(AnnouncementErrors.NotFound, (await Service(factory).SaveAsync(form)).Error);
    }

    [Fact]
    public async Task SetArchivedAsync_HidesIt_UntilRestored()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = Service(factory);
        var id = (await service.SaveAsync(Conference())).Value;

        Assert.True((await service.SetArchivedAsync(id, archived: true)).IsSuccess);
        Assert.Empty(await service.GetAllAsync(includeArchived: false));
        Assert.True(Assert.Single(await service.GetAllAsync(includeArchived: true)).IsArchived);

        await service.SetArchivedAsync(id, archived: false);
        Assert.Single(await service.GetAllAsync(includeArchived: false));
        Assert.Equal(AnnouncementErrors.NotFound, (await service.SetArchivedAsync(999_999, true)).Error);
    }

    [Fact]
    public async Task GetShownOnAsync_LeavesOutWhatIsOverOrArchived_InReadingOrder()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = Service(factory);
        async Task<int> Add(AnnouncementKind kind, string title, DateOnly? date = null, DateOnly? end = null) =>
            (await service.SaveAsync(new AnnouncementFormModel
            {
                Kind = kind, Title = title, EventDate = date, EndDate = end,
                Schedule = kind == AnnouncementKind.Recurring ? "Mercredi soir de 19h00 à 21h15" : null,
            })).Value;

        await Add(AnnouncementKind.External, "Réveil des dames", new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 8));
        await Add(AnnouncementKind.Event, "Soirée du ministère des hommes", new DateOnly(2026, 11, 28));
        await Add(AnnouncementKind.Pastoral, "Les gens malades");
        await Add(AnnouncementKind.Event, "Conférence", new DateOnly(2026, 10, 31));
        await Add(AnnouncementKind.Event, "Soirée du ministère des dames", new DateOnly(2026, 11, 21));
        await Add(AnnouncementKind.Recurring, "Réunion de prière");
        await service.SetArchivedAsync(await Add(AnnouncementKind.Recurring, "Ancienne activité"), archived: true);

        var shown = await service.GetShownOnAsync(new DateOnly(2026, 11, 8));

        // The conference (31 Oct) is over; the revival ends that very Sunday, so it stays.
        Assert.Equal(
            ["Réunion de prière", "Soirée du ministère des dames", "Soirée du ministère des hommes", "Les gens malades", "Réveil des dames"],
            shown.Select(a => a.Title));
    }

    [Fact]
    public async Task Theme_IsSavedPerMonth_ReplacedThenRemovedWhenBlank()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = Service(factory);

        await service.SaveThemeAsync(2026, 10, " Octobre : Mois d’actions de grâces ");
        await service.SaveThemeAsync(2026, 11, "Novembre : Mois de la famille");
        Assert.Equal("Octobre : Mois d’actions de grâces", await service.GetThemeAsync(2026, 10));

        await service.SaveThemeAsync(2026, 10, "Octobre : Mois de la reconnaissance");
        Assert.Equal("Octobre : Mois de la reconnaissance", await service.GetThemeAsync(2026, 10));

        await service.SaveThemeAsync(2026, 10, "  ");
        Assert.Null(await service.GetThemeAsync(2026, 10));
        Assert.Equal("Novembre : Mois de la famille", await service.GetThemeAsync(2026, 11));
    }

    [Fact]
    public async Task Theme_TooLong_IsRefused()
    {
        using var factory = new CustomWebApplicationFactory();

        var result = await Service(factory).SaveThemeAsync(2026, 10, new string('x', AnnouncementService.MaxThemeLength + 1));

        Assert.Equal(AnnouncementErrors.ThemeTooLong, result.Error);
    }
}
