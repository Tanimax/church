using ChurchAttendance.E2ETests.Infrastructure;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

[Collection("E2E")]
[Trait("Category", "E2E")]
public class MembersJourneyTests(AppFixture fixture)
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
    public async Task AddMember_ValidData_AppearsInList()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await LoginAsAdminAsync(page);

            var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
            var lastName = $"ZzzE2E{uniqueSuffix}";
            var firstName = "Jean";

            await BlazorInteraction.ClickUntilVisibleAsync(
                page, "button:has-text('+ Ajouter un membre')", page.Locator(".modal-header:has-text('Ajouter un membre')"));

            var textInputs = page.Locator(".modal-body input.form-control");
            await textInputs.Nth(0).FillAsync(firstName); // Prénom
            await textInputs.Nth(1).FillAsync(lastName); // Nom
            await page.Locator("#addIsBaptized").CheckAsync();

            await page.ClickAsync(".modal-footer button[type='submit']");

            await Expect(page.Locator(".modal-header")).ToContainTextAsync($"Membre créé : {firstName} {lastName}");

            await page.ClickAsync(".modal-footer button:has-text('Fermer')");
            await Expect(page.Locator(".modal")).ToHaveCountAsync(0);

            await page.FillAsync("input[placeholder='Rechercher par nom ou prénom...']", lastName);

            var row = page.Locator("table tbody tr", new() { HasText = lastName });
            await Expect(row).ToBeVisibleAsync();
            await Expect(row).ToContainTextAsync(firstName);
            await Expect(row).ToContainTextAsync("Oui"); // Baptisé column
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task AddMember_MissingRequiredFields_ShowsValidationAndBlocksSubmit()
    {
        var page = await fixture.Browser.NewPageAsync();
        try
        {
            await LoginAsAdminAsync(page);

            await BlazorInteraction.ClickUntilVisibleAsync(
                page, "button:has-text('+ Ajouter un membre')", page.Locator(".modal-header:has-text('Ajouter un membre')"));

            // Leave Prénom/Nom blank and submit directly.
            await page.ClickAsync(".modal-footer button[type='submit']");

            await Expect(page.Locator(".modal-body")).ToContainTextAsync("Le prénom est requis.");
            await Expect(page.Locator(".modal-body")).ToContainTextAsync("Le nom est requis.");

            // The modal must still be showing the form, not the success view.
            await Expect(page.Locator(".modal-header:has-text('Ajouter un membre')")).ToBeVisibleAsync();
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
