using ChurchAttendance.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

// The menu's groups (Rapports, Communication) open and close in the browser, and the
// breadcrumb leads back home.
[Collection("E2E")]
[Trait("Category", "E2E")]
public class NavigationJourneyTests(AppFixture fixture)
{
    [Fact]
    public async Task ReportsGroup_LeadsToAReport_AndTheBreadcrumbLeadsHome()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await E2EActions.LoginAsync(page, fixture.BaseUrl, "admin", E2EActions.AdminPassword);
            var visitors = page.Locator(".nav-scrollable a.nav-link", new() { HasText = "Visiteurs" });
            await Expect(visitors).ToBeHiddenAsync(); // the group is closed on the Membres page

            await page.ClickAsync(".nav-scrollable summary:has-text('Rapports')");
            await visitors.ClickAsync();

            await Expect(page).ToHaveURLAsync($"{fixture.BaseUrl}/admin/reports/visiteurs");
            await Expect(page.Locator("h1")).ToHaveTextAsync("Rapport Visiteurs");
            var breadcrumb = page.Locator("nav.app-breadcrumb");
            await Expect(breadcrumb.Locator("li")).ToHaveTextAsync(["Accueil", "Rapports", "Visiteurs"]);

            await breadcrumb.GetByRole(Microsoft.Playwright.AriaRole.Link, new() { Name = "Accueil" }).ClickAsync();
            await Expect(page).ToHaveURLAsync($"{fixture.BaseUrl}/");
            await Expect(page.Locator("h1")).ToHaveTextAsync("Tableau de bord");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task OnAPhone_OpeningAGroup_KeepsTheMenuOpen()
    {
        var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 800 } });
        try
        {
            var page = await context.NewPageAsync();
            await E2EActions.LoginAsync(page, fixture.BaseUrl, "admin", E2EActions.AdminPassword);

            await page.ClickAsync(".navbar-toggler");
            await page.ClickAsync(".nav-scrollable summary:has-text('Communication')");

            // A tap anywhere else in the menu closes it (it is a link); a group header must not.
            await Expect(page.Locator(".navbar-toggler")).ToBeCheckedAsync();
            await page.Locator(".nav-scrollable a.nav-link", new() { HasText = "Programmation" }).ClickAsync();
            await Expect(page).ToHaveURLAsync($"{fixture.BaseUrl}/admin/programmation");
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
