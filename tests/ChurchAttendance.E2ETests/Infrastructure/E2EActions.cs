using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Infrastructure;

// Steps shared by the journeys that need more than the bootstrap admin.
public static class E2EActions
{
    public const string AdminPassword = "dev-only-password";

    public static async Task LoginAsync(IPage page, string baseUrl, string username, string password)
    {
        await page.GotoAsync($"{baseUrl}/admin/login");
        await page.FillAsync("input[name='username']", username);
        await page.FillAsync("input[name='password']", password);
        await page.ClickAsync("button[type='submit']");
        await Expect(page).ToHaveURLAsync($"{baseUrl}/admin/members");
    }

    // Through the Utilisateurs page, as the admin logged in on adminPage.
    public static async Task CreateUserAsync(IPage adminPage, string baseUrl, string username, string password, string role)
    {
        await adminPage.GotoAsync($"{baseUrl}/admin/users");
        var modal = adminPage.Locator(".modal-content");
        await BlazorInteraction.ClickUntilVisibleAsync(adminPage, "button:has-text('+ Ajouter un utilisateur')", modal);
        await modal.Locator("input.form-control").Nth(0).FillAsync(username);
        await modal.Locator("input[type='password']").FillAsync(password);
        await modal.Locator("select").SelectOptionAsync(role);
        await modal.Locator("button[type='submit']").ClickAsync();
        await Expect(adminPage.Locator("table tbody tr", new() { HasText = username.ToLowerInvariant() })).ToBeVisibleAsync();
    }

    // Through POST /api/members, with the cookie of the user logged in on page.
    public static async Task CreateMemberAsync(IPage page, string baseUrl, string firstName, string lastName)
    {
        var response = await page.APIRequest.PostAsync($"{baseUrl}/api/members",
            new() { DataObject = new { firstName, lastName } });
        Assert.Equal(201, response.Status);
    }

    // Members whose name contains lastName, counted from the CSV export (one line per member
    // plus the header), so a check doesn't depend on the list's paging.
    public static async Task<int> CountMembersAsync(IPage page, string baseUrl, string lastName)
    {
        var response = await page.APIRequest.GetAsync($"{baseUrl}/admin/members/export.csv?search={Uri.EscapeDataString(lastName)}");
        var csv = await response.TextAsync();
        return csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1;
    }

    // Clicks the button `times` times in the same instant, from the page itself: what a
    // nervous double tap on a slow phone connection does. Playwright's own clicks wait for
    // each other and never reproduce it.
    public static Task BurstClickAsync(IPage page, string selector, int times) =>
        page.EvaluateAsync(
            "([selector, times]) => { const b = document.querySelector(selector); for (let i = 0; i < times; i++) b.click(); }",
            new object[] { selector, times });
}
