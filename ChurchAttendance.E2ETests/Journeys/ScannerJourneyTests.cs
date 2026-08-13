using ChurchAttendance.E2ETests.Infrastructure;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

// The scanner page (wwwroot/scanner/) is a plain HTML/JS PWA, not a Blazor component — the
// camera library (html5-qrcode) calls the global JS function onScanSuccess(decodedText), which
// itself calls showResult(status, fullName) to render the overlay. Rather than simulate a real
// camera, these tests call showResult directly from the page's JS context to verify the overlay
// rendering for each status. The /api/checkin logic itself is already covered by
// ChurchAttendance.Tests/Endpoints/CheckInEndpointsTests.
[Collection("E2E")]
[Trait("Category", "E2E")]
public class ScannerJourneyTests(AppFixture fixture)
{
    private async Task<IPage> LoginAndOpenScannerAsync()
    {
        var page = await fixture.Browser.NewPageAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/admin/login");
        await page.FillAsync("input[name='username']", "admin");
        await page.FillAsync("input[name='password']", "dev-only-password");
        await page.ClickAsync("button[type='submit']");
        await Expect(page).ToHaveURLAsync($"{fixture.BaseUrl}/admin/members");

        await page.GotoAsync($"{fixture.BaseUrl}/scanner/index.html");
        await Expect(page.Locator("#reader")).ToBeVisibleAsync();

        return page;
    }

    [Theory]
    [InlineData("ok", "Jean Dupont", "enregistrée")]
    [InlineData("duplicate", "Marie Curie", "Déjà enregistré")]
    [InlineData("not_baptized", "Paul Martin", "non baptisé")]
    [InlineData("not_found", "", "QR code inconnu")]
    public async Task ShowResult_EachStatus_RendersExpectedOverlay(string status, string fullName, string expectedMessageFragment)
    {
        var page = await LoginAndOpenScannerAsync();
        try
        {
            await page.EvaluateAsync("([status, fullName]) => showResult(status, fullName)", new[] { status, fullName });

            var overlay = page.Locator($"#result-overlay.show.{status}");
            await Expect(overlay).ToBeVisibleAsync();
            await Expect(overlay.Locator(".message")).ToContainTextAsync(expectedMessageFragment);

            if (!string.IsNullOrEmpty(fullName))
            {
                await Expect(overlay.Locator(".name")).ToHaveTextAsync(fullName);
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task ShowResult_Ok_AutoDismissesAfterTimeout()
    {
        var page = await LoginAndOpenScannerAsync();
        try
        {
            await page.EvaluateAsync("showResult('ok', 'Jean Dupont')");
            await Expect(page.Locator("#result-overlay.show.ok")).ToBeVisibleAsync();

            // showResult clears the overlay's class 2.5s after showing it.
            await Expect(page.Locator("#result-overlay.show")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
