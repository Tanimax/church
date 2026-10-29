using System.IO.Compression;
using System.Text.Json;
using ChurchAttendance.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

// The app runs here from its build output, where wwwroot isn't copied next to the DLL: what
// the server reads from wwwroot (card font, banner) must still be found.
[Collection("E2E")]
[Trait("Category", "E2E")]
public class MemberCardJourneyTests(AppFixture fixture)
{
    [Fact]
    public async Task CardPage_ShowsTheBanner_AndPrintedCardsDownload()
    {
        var lastName = $"ZzzCarte{Guid.NewGuid().ToString("N")[..8]}";
        var context = await fixture.Browser.NewContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            await E2EActions.LoginAsync(page, fixture.BaseUrl, "admin", E2EActions.AdminPassword);
            var created = await page.APIRequest.PostAsync($"{fixture.BaseUrl}/api/members",
                new() { DataObject = new { firstName = "Jean", lastName } });
            var member = JsonDocument.Parse(await created.TextAsync()).RootElement;

            // The member's card on a phone: the banner image is actually loaded.
            var card = await context.NewPageAsync();
            await card.GotoAsync($"{fixture.BaseUrl}/card/{member.GetProperty("token").GetString()}");
            var banner = card.Locator("img.banner");
            await Expect(banner).ToBeVisibleAsync();
            Assert.True(await banner.EvaluateAsync<bool>("img => img.complete && img.naturalWidth > 0"), "bandeau non chargé");

            // The printed cards (font and banner drawn by the server).
            var zip = await page.APIRequest.GetAsync($"{fixture.BaseUrl}/admin/members/cards.zip?ids={member.GetProperty("id").GetInt32()}");
            Assert.Equal(200, zip.Status);
            using var archive = new ZipArchive(new MemoryStream(await zip.BodyAsync()));
            Assert.Single(archive.Entries);
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
