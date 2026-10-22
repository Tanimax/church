using ChurchAttendance.E2ETests.Infrastructure;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

// Opens every admin page as an admin and checks it finishes loading without a Blazor error.
// Catches wiring mistakes (an unregistered service, a broken query) that compile fine
// but only fail when the page renders.
[Collection("E2E")]
[Trait("Category", "E2E")]
public class AdminPagesJourneyTests(AppFixture fixture)
{
    private async Task LoginAsAdminAsync(IPage page)
    {
        await page.GotoAsync($"{fixture.BaseUrl}/admin/login");
        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "dev-only-password");
        await page.ClickAsync("button[type='submit']");
        await Expect(page).ToHaveURLAsync($"{fixture.BaseUrl}/admin/members");
    }

    [Theory]
    [InlineData("/", "Tableau de bord")]
    [InlineData("/admin/members", "Membres")]
    [InlineData("/admin/users", "Utilisateurs")]
    [InlineData("/admin/ecole-dominicale", "École Dominicale")]
    [InlineData("/admin/reports", "Rapport Dominical")]
    [InlineData("/admin/reports/sainte-cene", "Rapport Sainte Cène")]
    [InlineData("/admin/reports/visiteurs", "Rapport Visiteurs")]
    [InlineData("/admin/reports/anniversaires", "Rapport d'anniversaires")]
    public async Task AdminPage_LoadsWithoutError(string path, string heading)
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await LoginAsAdminAsync(page);

            await page.GotoAsync($"{fixture.BaseUrl}{path}");

            await Expect(page.Locator("h1")).ToHaveTextAsync(heading);
            await Expect(page.GetByText("Chargement...")).ToHaveCountAsync(0);
            await Expect(page.Locator("#blazor-error-ui")).ToBeHiddenAsync();
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
