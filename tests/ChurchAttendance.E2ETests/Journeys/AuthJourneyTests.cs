using ChurchAttendance.E2ETests.Infrastructure;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

[Collection("E2E")]
[Trait("Category", "E2E")]
public class AuthJourneyTests(AppFixture fixture)
{
    [Fact]
    public async Task Login_ValidBootstrapAdmin_LandsOnMembers()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.BaseUrl}/admin/login");
            await page.FillAsync("input[name='username']", "admin");
            await page.FillAsync("input[name='password']", "dev-only-password");
            await page.ClickAsync("button[type='submit']");

            await Expect(page).ToHaveURLAsync($"{fixture.BaseUrl}/admin/members");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Login_WrongPassword_ShowsErrorAndStaysOnLogin()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.BaseUrl}/admin/login");
            await page.FillAsync("input[name='username']", "admin");
            await page.FillAsync("input[name='password']", "wrong-password");
            await page.ClickAsync("button[type='submit']");

            await Expect(page.Locator(".error")).ToBeVisibleAsync();
            await Expect(page.Locator(".error")).ToContainTextAsync("incorrect");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Login_UnknownUsername_ShowsError()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.BaseUrl}/admin/login");
            await page.FillAsync("input[name='username']", "does-not-exist");
            await page.FillAsync("input[name='password']", "whatever");
            await page.ClickAsync("button[type='submit']");

            await Expect(page.Locator(".error")).ToBeVisibleAsync();
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task Scanner_WithoutAuth_RedirectsToLoginThenBackToScannerAfterSignIn()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await page.GotoAsync($"{fixture.BaseUrl}/scanner");

            // The auth-gate middleware redirected us to the login page with a returnUrl.
            await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/admin/login\?returnUrl="));

            await page.FillAsync("input[name='username']", "admin");
            await page.FillAsync("input[name='password']", "dev-only-password");
            await page.ClickAsync("button[type='submit']");

            // Successful login bounced back to /scanner, which itself redirects to the real page.
            await Expect(page).ToHaveURLAsync($"{fixture.BaseUrl}/scanner/index.html");
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
