using ChurchAttendance.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

// Entering an announcement on the Annonces page, the way the secretariat does it.
[Collection("E2E")]
[Trait("Category", "E2E")]
public class AnnoncesJourneyTests(AppFixture fixture)
{
    [Fact]
    public async Task NewEvent_SavedWithABurstOfClicks_IsListedOnce_AndCanBeArchived()
    {
        var title = $"ZzzSoirée {Guid.NewGuid().ToString("N")[..8]}";
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await E2EActions.LoginAsync(page, fixture.BaseUrl, "admin", E2EActions.AdminPassword);
            await page.GotoAsync($"{fixture.BaseUrl}/admin/annonces");

            var modal = page.Locator(".modal-content");
            await BlazorInteraction.ClickUntilVisibleAsync(page, "button:has-text('+ Nouvelle annonce')", modal);
            await modal.Locator("#eventDate").FillAsync(date.ToString("yyyy-MM-dd"));
            await modal.Locator("#eventTime").FillAsync("à 18h00");
            await modal.Locator("#title").FillAsync(title);
            await modal.Locator("#title").BlurAsync(); // inputs bind on change

            await E2EActions.BurstClickAsync(page, ".modal-footer .btn-primary", 5);

            await Expect(modal).ToHaveCountAsync(0);
            await page.WaitForTimeoutAsync(1500); // let any extra click reach the database
            await page.ReloadAsync();
            var item = page.Locator(".announcement", new() { HasText = title });
            await Expect(item).ToHaveCountAsync(1);
            await Expect(item.Locator(".when")).ToContainTextAsync("à 18h00 :");

            await item.Locator("button:has-text('Archiver')").ClickAsync();
            await Expect(item).ToHaveCountAsync(0);
            await page.Locator("#showArchived").CheckAsync();
            await Expect(page.Locator(".announcement", new() { HasText = title }).Locator(".badge")).ToHaveTextAsync("Archivée");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task PowerPoint_DownloadsFromThePage()
    {
        // From the build output, as in the other journeys: the template (embedded) and the
        // banner (wwwroot) must both be found.
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await E2EActions.LoginAsync(page, fixture.BaseUrl, "admin", E2EActions.AdminPassword);
            await page.GotoAsync($"{fixture.BaseUrl}/admin/annonces");

            var download = await page.RunAndWaitForDownloadAsync(() => page.ClickAsync("a:has-text('Télécharger le PowerPoint')"));

            Assert.Matches(@"^Annonces_du_dimanche_.+\.pptx$", download.SuggestedFilename);
            var path = await download.PathAsync();
            Assert.Equal("PK"u8.ToArray(), (await File.ReadAllBytesAsync(path))[..2]);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
