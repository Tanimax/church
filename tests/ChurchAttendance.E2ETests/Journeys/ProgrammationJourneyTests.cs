using ChurchAttendance.E2ETests.Infrastructure;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

// The secretariat fills in next quarter's Sunday services and finds them again later.
[Collection("E2E")]
[Trait("Category", "E2E")]
public class ProgrammationJourneyTests(AppFixture fixture)
{
    [Fact]
    public async Task FillNextQuarter_Save_ThenFindItAgain()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var leader = $"Sr Margarette {suffix}";
        var preacher = $"Pasteur Onis {suffix}";
        var context = await fixture.Browser.NewContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            await E2EActions.LoginAsync(page, fixture.BaseUrl, "admin", E2EActions.AdminPassword);
            await page.GotoAsync($"{fixture.BaseUrl}/admin/programmation");
            var heading = page.Locator(".fw-bold:has-text('Service dominical')");
            var currentQuarter = await heading.InnerTextAsync();

            await BlazorInteraction.ClickUntilVisibleAsync(page, "button:has-text('Trimestre suivant')",
                page.Locator($".fw-bold:not(:has-text('{currentQuarter}'))").Filter(new() { HasText = "Service dominical" }));
            var nextQuarter = await heading.InnerTextAsync();

            var firstRow = page.Locator("table.schedule tbody tr").First;
            await firstRow.Locator("input").Nth(0).FillAsync(leader);
            await firstRow.Locator("input").Nth(1).FillAsync(preacher);
            await firstRow.Locator("input").Nth(1).BlurAsync(); // @bind updates on change
            await page.ClickAsync("button:has-text('Enregistrer')");
            await Expect(page.Locator(".alert-success")).ToContainTextAsync("Programmation enregistrée.");

            // The saved quarter can be downloaded as the printable sheet.
            var pdfLink = page.Locator("a:has-text('Télécharger le PDF')");
            await Expect(pdfLink).ToBeVisibleAsync();
            var pdf = await page.APIRequest.GetAsync($"{fixture.BaseUrl}{await pdfLink.GetAttributeAsync("href")}");
            Assert.Equal(200, pdf.Status);
            Assert.Equal("application/pdf", pdf.Headers["content-type"]);

            // Back later: the page opens on the current quarter; go to the next one again.
            await page.ReloadAsync();
            await BlazorInteraction.ClickUntilVisibleAsync(page, "button:has-text('Trimestre suivant')",
                page.Locator(".fw-bold", new() { HasText = nextQuarter }));
            var reloadedRow = page.Locator("table.schedule tbody tr").First;
            await Expect(reloadedRow.Locator("input").Nth(0)).ToHaveValueAsync(leader);
            await Expect(reloadedRow.Locator("input").Nth(1)).ToHaveValueAsync(preacher);

            // Names already used are suggested while typing.
            await Expect(page.Locator($"datalist#schedule-names option[value='{leader}']")).ToHaveCountAsync(1);
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
