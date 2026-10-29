using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using ChurchAttendance.Application.Announcements;
using ChurchAttendance.Features.Announcements;
using ChurchAttendance.Startup;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Components;
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

    private IRenderedComponent<Annonces> Render()
    {
        var page = _bunit.Render<Annonces>();
        page.WaitForAssertion(() => Assert.Empty(page.FindAll("em:contains('Chargement')")));
        return page;
    }

    private static void OpenNew(IRenderedComponent<Annonces> page) =>
        page.Find("button:contains('+ Nouvelle annonce')").Click();

    private static IElement Save(IRenderedComponent<Annonces> page) =>
        page.Find(".modal-footer .btn-primary");

    [Fact]
    public void NewEvent_IsSaved_AndListedWithItsWhenLine()
    {
        var page = Render();
        OpenNew(page);

        page.Find("#title").Change("Conférence du ministère des hommes et celui des dames.");
        page.Find("#eventDate").Change("2026-10-31");
        page.Find("#eventTime").Change("à 18h00");
        page.Find("#details").Change("Sujet : L’identité des ouvriers dans l’Église.\nSoyez des nôtres !");
        Save(page).Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".modal-content")));
        var item = page.Find(".announcement");
        Assert.Equal("Samedi 31 octobre 2026, à 18h00 :", item.QuerySelector(".when")!.TextContent);
        Assert.Equal("Sujet :", item.QuerySelector(".detail .label")!.TextContent);
        Assert.Equal(2, item.QuerySelectorAll(".detail").Length);
        Assert.Contains("1 annonce(s)", page.Markup);
    }

    [Fact]
    public void Dated_WithoutADate_IsRefused_AndTheFormStaysOpen()
    {
        var page = Render();
        OpenNew(page);

        page.Find("#title").Change("Soirée du ministère des dames.");
        Save(page).Click();

        page.WaitForAssertion(() => Assert.Contains(AnnouncementErrors.EventDateRequired.Message, page.Find(".modal-body").TextContent));
        Assert.Empty(page.FindAll(".announcement"));
    }

    [Fact]
    public async Task EveryWeek_ShowsTheScheduleInsteadOfTheDate_AndSavesARecurringActivity()
    {
        var page = Render();
        OpenNew(page);

        page.Find("#weekly").Change(true);
        Assert.Empty(page.FindAll("#eventDate"));
        page.Find("#schedule").Change("Vendredi soir de 19h00 à 21h30");
        page.Find("#title").Change("Répétition de la chorale");
        Save(page).Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".modal-content")));
        Assert.Equal(AnnouncementKind.Recurring, Assert.Single(await Service.GetAllAsync(includeArchived: false)).Kind);
    }

    [Fact]
    public void Preview_ShowsTheAnnouncementAsItsSlideWillRead()
    {
        var page = Render();
        OpenNew(page);

        page.Find("#title").Change("L'Église Par la foi nous invite au réveil des dames.");
        page.Find("#eventDate").Change("2026-11-06");
        page.Find("#endDate").Change("2026-11-08");
        page.Find("#external").Change(true);
        page.Find("#details").Change("Sujet : Jean 14 : 1-3");

        var preview = page.Find(".preview");
        Assert.Equal(AnnouncementTexts.ExternalHeading, preview.QuerySelector(".preview-heading")!.TextContent);
        Assert.Equal("Du vendredi 6 au dimanche 8 novembre 2026 :", preview.QuerySelector(".when")!.TextContent);
        Assert.Equal("Sujet :", preview.QuerySelector(".detail .label")!.TextContent);

        page.Find(".modal-footer .btn-secondary").Click();
        Assert.Empty(page.FindAll(".modal-content"));
    }

    [Fact]
    public async Task Edit_ChangesTheAnnouncement()
    {
        await Service.SaveAsync(new AnnouncementFormModel
        {
            Kind = AnnouncementKind.Recurring, Schedule = "Vendredi soir de 19h00 à 21h30", Title = "Répétition de la chorale",
        });
        var page = Render();

        page.Find("button:contains('Modifier')").Click();
        Assert.Equal("Répétition de la chorale", page.Find("#title").GetAttribute("value"));
        page.Find("#schedule").Change("Samedi matin de 9h00 à 11h00");
        Save(page).Click();

        page.WaitForAssertion(() => Assert.Equal("Samedi matin de 9h00 à 11h00 :", page.Find(".when").TextContent));
    }

    [Fact]
    public async Task Archive_HidesIt_AndTheArchivedViewShowsIt_ForRestoring()
    {
        await Service.SaveAsync(new AnnouncementFormModel { Kind = AnnouncementKind.Pastoral, Title = "Les gens malades" });
        var page = Render();

        page.Find("button:contains('Archiver')").Click();
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".announcement")));

        page.Find("#showArchived").Change(true);
        page.WaitForAssertion(() => Assert.Equal("Archivée", page.Find(".announcement .badge").TextContent));

        page.Find("button:contains('Restaurer')").Click();
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".announcement .badge")));
        Assert.False(Assert.Single(await Service.GetAllAsync(includeArchived: false)).IsArchived);
    }

    [Fact]
    public async Task PastEvent_IsMarkedPassee()
    {
        await Service.SaveAsync(new AnnouncementFormModel
        {
            Kind = AnnouncementKind.Event, EventDate = new DateOnly(2026, 10, 18), Title = "Journée missionnaire",
        });

        var page = Render();

        Assert.Equal("Passée", page.Find(".announcement .badge").TextContent);
        Assert.Contains("0 annonce(s)", page.Markup);
    }

    [Fact]
    public async Task Theme_IsSavedForTheUpcomingSundaysMonth()
    {
        var page = Render();
        Assert.Contains("Thème de octobre 2026", page.Markup);

        page.Find("#theme").Change("Octobre : Mois d’actions de grâces");
        page.Find("button:contains('Enregistrer le thème')").Click();

        page.WaitForAssertion(() => Assert.Contains("Thème enregistré.", page.Markup));
        Assert.Equal("Octobre : Mois d’actions de grâces", await Service.GetThemeAsync(2026, 10));
    }

    [Fact]
    public void Theme_TooLong_ShowsWhy()
    {
        var page = Render();

        page.Find("#theme").Change(new string('x', AnnouncementService.MaxThemeLength + 1));
        page.Find("button:contains('Enregistrer le thème')").Click();

        page.WaitForAssertion(() => Assert.Contains(AnnouncementErrors.ThemeTooLong.Message, page.Markup));
    }

    [Fact]
    public async Task WithoutTheCommunicationPolicy_EveryActionIsRefused()
    {
        // A hidden or stale button can still be clicked: each handler checks the policy again.
        await Service.SaveAsync(new AnnouncementFormModel { Kind = AnnouncementKind.Pastoral, Title = "Les gens malades" });
        var page = Render();
        _auth.SetPolicies();

        page.Find("button:contains('Archiver')").Click();
        page.WaitForAssertion(() => Assert.Contains("Accès refusé", page.Markup));

        page.Find("#theme").Change("Octobre");
        page.Find("button:contains('Enregistrer le thème')").Click();
        OpenNew(page);
        page.Find("#eventDate").Change("2026-10-31");
        page.Find("#title").Change("Conférence");
        Save(page).Click();
        page.WaitForAssertion(() => Assert.Contains("Accès refusé", page.Find(".modal-body").TextContent));

        await using var db = _factory.CreateDbContext();
        Assert.False((await db.Announcements.SingleAsync()).IsArchived);
        Assert.Single(db.Announcements);
        Assert.Empty(db.MonthlyThemes);
    }

    [Fact]
    public async Task Sunday_CanBeChanged_ToDownloadItsPowerPoint_WithItsMonthsTheme()
    {
        await Service.SaveThemeAsync(2026, 11, "Novembre : Mois de la famille");
        var page = Render();
        Assert.Equal("/admin/annonces/2026-10-25/pptx", page.Find("a:contains('Télécharger le PowerPoint')").GetAttribute("href"));

        page.Find("#sunday").Change("2026-11-01");

        page.WaitForAssertion(() => Assert.Equal("Novembre : Mois de la famille", page.Find("#theme").GetAttribute("value")));
        Assert.Equal("/admin/annonces/2026-11-01/pptx", page.Find("a:contains('Télécharger le PowerPoint')").GetAttribute("href"));
        Assert.Contains("Thème de novembre 2026", page.Markup);
    }

    [Fact]
    public void Sunday_SetToAnotherDay_AsksForASunday()
    {
        var page = Render();

        page.Find("#sunday").Change("2026-10-28");

        page.WaitForAssertion(() => Assert.Contains("Choisissez un dimanche.", page.Markup));
        Assert.Empty(page.FindAll("a:contains('Télécharger le PowerPoint')"));
    }
}
