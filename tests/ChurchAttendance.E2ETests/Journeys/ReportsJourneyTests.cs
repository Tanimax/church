using ChurchAttendance.E2ETests.Infrastructure;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

[Collection("E2E")]
[Trait("Category", "E2E")]
public class ReportsJourneyTests(AppFixture fixture)
{
    private async Task LoginAsAdminAsync(IPage page)
    {
        await page.GotoAsync($"{fixture.BaseUrl}/admin/login");
        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "dev-only-password");
        await page.ClickAsync("button[type='submit']");
        await Expect(page).ToHaveURLAsync($"{fixture.BaseUrl}/admin/members");
    }

    [Fact]
    public async Task SundayReport_LoadsPresencesAndAbsentMembers()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await LoginAsAdminAsync(page);

            await page.GotoAsync($"{fixture.BaseUrl}/admin/reports");

            await Expect(page.Locator("h1")).ToHaveTextAsync("Rapport Dominical");
            // The summary line only renders once the report data has loaded without error.
            await Expect(page.Locator("p", new() { HasText = "présent(s) le" })).ToBeVisibleAsync();
            await Expect(page.Locator("h4", new() { HasText = "Membres absents ce dimanche" })).ToBeVisibleAsync();
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
