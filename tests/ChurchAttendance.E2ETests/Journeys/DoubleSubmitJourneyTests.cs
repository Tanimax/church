using ChurchAttendance.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

// A burst of clicks on a "create" button used to create one record per click: Blazor runs
// the next click's handler as soon as the previous one awaits the database. Only a real
// browser and circuit reproduce it.
[Collection("E2E")]
[Trait("Category", "E2E")]
public class DoubleSubmitJourneyTests(AppFixture fixture)
{
    [Fact]
    public async Task BurstOfClicksOnCreerLaCarte_CreatesOneMember()
    {
        var lastName = $"ZzzBurst{Guid.NewGuid().ToString("N")[..8]}";
        var context = await fixture.Browser.NewContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            await E2EActions.LoginAsync(page, fixture.BaseUrl, "admin", E2EActions.AdminPassword);

            await BlazorInteraction.ClickUntilVisibleAsync(
                page, "button:has-text('+ Ajouter un membre')", page.Locator(".modal-header:has-text('Ajouter un membre')"));
            var inputs = page.Locator(".modal-body input.form-control");
            await inputs.Nth(0).FillAsync("Jean");
            await inputs.Nth(1).FillAsync(lastName);
            await inputs.Nth(1).BlurAsync(); // InputText binds on change

            await E2EActions.BurstClickAsync(page, ".modal-footer button[type='submit']", 5);

            await Expect(page.Locator(".modal-header")).ToContainTextAsync($"Membre créé : Jean {lastName}");
            await page.WaitForTimeoutAsync(1500); // let any extra click reach the database
            Assert.Equal(1, await E2EActions.CountMembersAsync(page, fixture.BaseUrl, lastName));
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task BurstOfClicksOnCreerLaClasse_CreatesOneClass()
    {
        var className = $"ZzzClasse{Guid.NewGuid().ToString("N")[..8]}";
        var context = await fixture.Browser.NewContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            await E2EActions.LoginAsync(page, fixture.BaseUrl, "admin", E2EActions.AdminPassword);
            await page.GotoAsync($"{fixture.BaseUrl}/admin/ecole-dominicale");

            var modal = page.Locator(".modal-content");
            await BlazorInteraction.ClickUntilVisibleAsync(page, "button:has-text('+ Ajouter une classe')", modal);
            var name = modal.Locator("input.form-control").First;
            await name.FillAsync(className);
            await name.BlurAsync();

            await E2EActions.BurstClickAsync(page, ".modal-footer button[type='submit']", 5);

            await Expect(page.Locator(".modal-content")).ToHaveCountAsync(0);
            await page.WaitForTimeoutAsync(1500);
            await page.ReloadAsync();
            await Expect(page.Locator("table tbody tr", new() { HasText = className })).ToHaveCountAsync(1);
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
