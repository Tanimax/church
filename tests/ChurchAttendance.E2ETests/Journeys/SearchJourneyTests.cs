using ChurchAttendance.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

// Names are typed the quick way, on a phone: no accents, no hyphens (TextSearch).
[Collection("E2E")]
[Trait("Category", "E2E")]
public class SearchJourneyTests(AppFixture fixture)
{
    [Theory]
    [InlineData("/admin/members", "input[placeholder='Rechercher par nom ou prénom...']", "table tbody tr")]
    [InlineData("/admin/sainte-cene-manuelle", ".sc-search", ".sc-card")]
    public async Task SearchWithoutAccentsOrHyphens_FindsTheMember(string path, string searchBox, string result)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var context = await fixture.Browser.NewContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            await E2EActions.LoginAsync(page, fixture.BaseUrl, "admin", E2EActions.AdminPassword);
            await E2EActions.CreateMemberAsync(page, fixture.BaseUrl, "Éloïse-Marie", $"Côté{suffix}");

            await page.GotoAsync($"{fixture.BaseUrl}{path}");
            await page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);
            await page.FillAsync(searchBox, $"eloise marie cote{suffix}");

            await Expect(page.Locator(result, new() { HasText = $"Côté{suffix}" })).ToBeVisibleAsync();
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
