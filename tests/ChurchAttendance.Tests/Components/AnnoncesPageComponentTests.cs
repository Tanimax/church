using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using ChurchAttendance.Application.Announcements;
using ChurchAttendance.Features.Announcements;
using ChurchAttendance.Startup;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Tests.Components;

// The Annonces page's actions (new, edit, archive, the month's theme) run in the component, on
// the real AnnouncementService and database: bUnit renders it here, without a browser.
public sealed class AnnoncesPageComponentTests : IDisposable
{
    // Saturday 24 October 2026: the announcements are prepared for Sunday the 25th.
    private readonly CustomWebApplicationFactory _factory = new(new FixedTimeProvider(FixedTimeProvider.SaturdayEveningInMontreal));
    private readonly IServiceScope _scope;
    private readonly BunitContext _bunit = new();
    private readonly BunitAuthorizationContext _auth;

    public AnnoncesPageComponentTests()
    {
        _scope = _factory.Services.CreateScope();
        _bunit.Services.AddSingleton(_scope.ServiceProvider.GetRequiredService<AnnouncementService>());
        _auth = _bunit.AddAuthorization();
        _auth.SetAuthorized("secretaire");
        _auth.SetPolicies(AuthorizationPolicies.ManageCommunication);
        // Last: it builds the renderer, after which no service can be added.
        _bunit.SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    public void Dispose()
    {
        _bunit.Dispose();
        _scope.Dispose();
        _factory.Dispose();
    }

    private AnnouncementService Service => _scope.ServiceProvider.GetRequiredService<AnnouncementService>();

    private async Task<IRenderedComponent<Annonces>> RenderAsync()
    {
        var page = _bunit.Render<Annonces>();
        await page.WaitForAssertionAsync(() => Assert.Empty(page.FindAll("em:contains('Chargement')")));
        return page;
    }

    private static Task OpenNewAsync(IRenderedComponent<Annonces> page) =>
        page.Find("button:contains('+ Nouvelle annonce')").ClickAsync(new MouseEventArgs());

    private static IElement Save(IRenderedComponent<Annonces> page) =>
        page.Find(".modal-footer .btn-primary");

    [Fact]
    public async Task NewEvent_IsSaved_AndListedWithItsWhenLine()
    {
        var page = await RenderAsync();
        await OpenNewAsync(page);

        await page.Find("#title").ChangeAsync("Conférence du ministère des hommes et celui des dames.");
        await page.Find("#eventDate").ChangeAsync("2026-10-31");
        await page.Find("#eventTime").ChangeAsync("à 18h00");
        await page.Find("#details").ChangeAsync("Sujet : L’identité des ouvriers dans l’Église.\nSoyez des nôtres !");
        await Save(page).ClickAsync(new MouseEventArgs());

        await page.WaitForAssertionAsync(() => Assert.Empty(page.FindAll(".modal-content")));
        var item = page.Find(".announcement");
        Assert.Equal("Samedi 31 octobre 2026, à 18h00 :", item.QuerySelector(".when")!.TextContent);
        Assert.Equal("Sujet :", item.QuerySelector(".detail .label")!.TextContent);
        Assert.Equal(2, item.QuerySelectorAll(".detail").Length);
        Assert.Contains("1 annonce(s)", page.Markup);
    }

    [Fact]
    public async Task Dated_WithoutADate_IsRefused_AndTheFormStaysOpen()
    {
        var page = await RenderAsync();
        await OpenNewAsync(page);

        await page.Find("#title").ChangeAsync("Soirée du ministère des dames.");
        await Save(page).ClickAsync(new MouseEventArgs());

        await page.WaitForAssertionAsync(() => Assert.Contains(AnnouncementErrors.EventDateRequired.Message, page.Find(".modal-body").TextContent));
        Assert.Empty(page.FindAll(".announcement"));
    }

    [Fact]
    public async Task EveryWeek_ShowsTheScheduleInsteadOfTheDate_AndSavesARecurringActivity()
    {
        var page = await RenderAsync();
        await OpenNewAsync(page);

        await page.Find("#weekly").ChangeAsync(true);
        Assert.Empty(page.FindAll("#eventDate"));
        await page.Find("#schedule").ChangeAsync("Vendredi soir de 19h00 à 21h30");
        await page.Find("#title").ChangeAsync("Répétition de la chorale");
        await Save(page).ClickAsync(new MouseEventArgs());

        await page.WaitForAssertionAsync(() => Assert.Empty(page.FindAll(".modal-content")));
        Assert.Equal(AnnouncementKind.Recurring, Assert.Single(await Service.GetAllAsync(includeArchived: false)).Kind);
    }

    [Fact]
    public async Task Edit_AnExternalAnnouncement_KeepsItExternal()
    {
        // Entered when the form still had an "another church" box: editing it doesn't change that.
        await Service.SaveAsync(new AnnouncementFormModel
        {
            Kind = AnnouncementKind.External, EventDate = new DateOnly(2026, 11, 6), Title = "Réveil des dames",
        });
        var page = await RenderAsync();

        await page.Find("button:contains('Modifier')").ClickAsync(new MouseEventArgs());
        await page.Find("#title").ChangeAsync("Réveil des dames à l'Église Par la foi");
        await Save(page).ClickAsync(new MouseEventArgs());

        await page.WaitForAssertionAsync(() => Assert.Empty(page.FindAll(".modal-content")));
        var saved = Assert.Single(await Service.GetAllAsync(includeArchived: false));
        Assert.Equal((AnnouncementKind.External, "Réveil des dames à l'Église Par la foi"), (saved.Kind, saved.Title));
    }

    [Fact]
    public async Task Preview_ShowsTheAnnouncementAsItsSlideWillRead()
    {
        var page = await RenderAsync();
        await OpenNewAsync(page);

        await page.Find("#title").ChangeAsync("L'Église Par la foi nous invite au réveil des dames.");
        await page.Find("#eventDate").ChangeAsync("2026-11-06");
        await page.Find("#endDate").ChangeAsync("2026-11-08");
        await page.Find("#heading").ChangeAsync(AnnouncementTexts.ExternalHeading);
        await page.Find("#details").ChangeAsync("Sujet : Jean 14 : 1-3");

        Assert.Empty(page.FindAll("#external")); // no "another church" box: the heading says it
        var preview = page.Find(".preview");
        Assert.Equal(AnnouncementTexts.ExternalHeading, preview.QuerySelector(".preview-heading")!.TextContent);
        Assert.Equal("Du vendredi 6 au dimanche 8 novembre 2026 :", preview.QuerySelector(".when")!.TextContent);
        Assert.Equal("Sujet :", preview.QuerySelector(".detail .label")!.TextContent);

        await page.Find(".modal-footer .btn-secondary").ClickAsync(new MouseEventArgs());
        Assert.Empty(page.FindAll(".modal-content"));
    }

    [Fact]
    public async Task Edit_ChangesTheAnnouncement()
    {
        await Service.SaveAsync(new AnnouncementFormModel
        {
            Kind = AnnouncementKind.Recurring, Schedule = "Vendredi soir de 19h00 à 21h30", Title = "Répétition de la chorale",
        });
        var page = await RenderAsync();

        await page.Find("button:contains('Modifier')").ClickAsync(new MouseEventArgs());
        Assert.Equal("Répétition de la chorale", page.Find("#title").GetAttribute("value"));
        await page.Find("#schedule").ChangeAsync("Samedi matin de 9h00 à 11h00");
        await Save(page).ClickAsync(new MouseEventArgs());

        await page.WaitForAssertionAsync(() => Assert.Equal("Samedi matin de 9h00 à 11h00 :", page.Find(".when").TextContent));
    }

    [Fact]
    public async Task Archive_HidesIt_AndTheArchivedViewShowsIt_ForRestoring()
    {
        await Service.SaveAsync(new AnnouncementFormModel { Kind = AnnouncementKind.Pastoral, Title = "Les gens malades" });
        var page = await RenderAsync();

        await page.Find("button:contains('Archiver')").ClickAsync(new MouseEventArgs());
        await page.WaitForAssertionAsync(() => Assert.Empty(page.FindAll(".announcement")));

        await page.Find("#showArchived").ChangeAsync(true);
        await page.WaitForAssertionAsync(() => Assert.Equal("Archivée", page.Find(".announcement .badge").TextContent));

        await page.Find("button:contains('Restaurer')").ClickAsync(new MouseEventArgs());
        await page.WaitForAssertionAsync(() => Assert.Empty(page.FindAll(".announcement .badge")));
        Assert.False(Assert.Single(await Service.GetAllAsync(includeArchived: false)).IsArchived);
    }

    [Fact]
    public async Task PastEvent_IsMarkedPassee()
    {
        await Service.SaveAsync(new AnnouncementFormModel
        {
            Kind = AnnouncementKind.Event, EventDate = new DateOnly(2026, 10, 18), Title = "Journée missionnaire",
        });

        var page = await RenderAsync();

        Assert.Equal("Passée", page.Find(".announcement .badge").TextContent);
        Assert.Contains("0 annonce(s)", page.Markup);
    }

    [Fact]
    public async Task Theme_IsSavedForTheUpcomingSundaysMonth()
    {
        var page = await RenderAsync();
        Assert.Contains("Thème de octobre 2026", page.Markup);

        await page.Find("#theme").ChangeAsync("Octobre : Mois d’actions de grâces");
        await page.Find("button:contains('Enregistrer le thème')").ClickAsync(new MouseEventArgs());

        await page.WaitForAssertionAsync(() => Assert.Contains("Thème enregistré.", page.Markup));
        Assert.Equal("Octobre : Mois d’actions de grâces", await Service.GetThemeAsync(2026, 10));
    }

    [Fact]
    public async Task Theme_TooLong_ShowsWhy()
    {
        var page = await RenderAsync();

        await page.Find("#theme").ChangeAsync(new string('x', AnnouncementService.MaxThemeLength + 1));
        await page.Find("button:contains('Enregistrer le thème')").ClickAsync(new MouseEventArgs());

        await page.WaitForAssertionAsync(() => Assert.Contains(AnnouncementErrors.ThemeTooLong.Message, page.Markup));
    }

    [Fact]
    public async Task WithoutTheCommunicationPolicy_EveryActionIsRefused()
    {
        // A hidden or stale button can still be clicked: each handler checks the policy again.
        await Service.SaveAsync(new AnnouncementFormModel { Kind = AnnouncementKind.Pastoral, Title = "Les gens malades" });
        var page = await RenderAsync();
        _auth.SetPolicies();

        await page.Find("button:contains('Archiver')").ClickAsync(new MouseEventArgs());
        await page.WaitForAssertionAsync(() => Assert.Contains("Accès refusé", page.Markup));

        await page.Find("#theme").ChangeAsync("Octobre");
        await page.Find("button:contains('Enregistrer le thème')").ClickAsync(new MouseEventArgs());
        await OpenNewAsync(page);
        await page.Find("#eventDate").ChangeAsync("2026-10-31");
        await page.Find("#title").ChangeAsync("Conférence");
        await Save(page).ClickAsync(new MouseEventArgs());
        await page.WaitForAssertionAsync(() => Assert.Contains("Accès refusé", page.Find(".modal-body").TextContent));

        await using var db = _factory.CreateDbContext();
        Assert.False((await db.Announcements.SingleAsync()).IsArchived);
        Assert.Single(db.Announcements);
        Assert.Empty(db.MonthlyThemes);
    }

    [Fact]
    public async Task Sunday_CanBeChanged_ToDownloadItsPowerPoint_WithItsMonthsTheme()
    {
        await Service.SaveThemeAsync(2026, 11, "Novembre : Mois de la famille");
        var page = await RenderAsync();
        Assert.Equal("/admin/annonces/2026-10-25/pptx", page.Find("a:contains('Télécharger le PowerPoint')").GetAttribute("href"));

        await page.Find("#sunday").ChangeAsync("2026-11-01");

        await page.WaitForAssertionAsync(() => Assert.Equal("Novembre : Mois de la famille", page.Find("#theme").GetAttribute("value")));
        Assert.Equal("/admin/annonces/2026-11-01/pptx", page.Find("a:contains('Télécharger le PowerPoint')").GetAttribute("href"));
        Assert.Contains("Thème de novembre 2026", page.Markup);
    }

    [Fact]
    public async Task Sunday_SetToAnotherDay_AsksForASunday()
    {
        var page = await RenderAsync();

        await page.Find("#sunday").ChangeAsync("2026-10-28");

        await page.WaitForAssertionAsync(() => Assert.Contains("Choisissez un dimanche.", page.Markup));
        Assert.Empty(page.FindAll("a:contains('Télécharger le PowerPoint')"));
    }
}
