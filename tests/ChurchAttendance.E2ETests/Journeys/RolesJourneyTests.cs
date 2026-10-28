using ChurchAttendance.E2ETests.Infrastructure;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Journeys;

// Who may manage members (AuthorizationPolicies.ManageMembers: Admin and École du dimanche),
// checked where a real user meets it: the buttons they see and the API they can call.
[Collection("E2E")]
[Trait("Category", "E2E")]
public class RolesJourneyTests(AppFixture fixture)
{
    [Fact]
    public async Task Secretaire_SeesMembers_ButCannotAddEditOrDeactivate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var secretaire = $"sec{suffix}";
        var lastName = $"ZzzRole{suffix}";

        var adminContext = await fixture.Browser.NewContextAsync();
        var secretaireContext = await fixture.Browser.NewContextAsync();
        try
        {
            var admin = await adminContext.NewPageAsync();
            await E2EActions.LoginAsync(admin, fixture.BaseUrl, "admin", E2EActions.AdminPassword);
            await E2EActions.CreateMemberAsync(admin, fixture.BaseUrl, "Jean", lastName);
            await E2EActions.CreateUserAsync(admin, fixture.BaseUrl, secretaire, "motdepasse123", "Secretaire");

            var page = await secretaireContext.NewPageAsync();
            await E2EActions.LoginAsync(page, fixture.BaseUrl, secretaire, "motdepasse123");
            await page.FillAsync("input[placeholder='Rechercher par nom ou prénom...']", lastName);

            var row = page.Locator("table tbody tr", new() { HasText = lastName });
            await Expect(row).ToBeVisibleAsync();
            await Expect(row.Locator("button:has-text('Voir la carte')")).ToBeVisibleAsync();
            await Expect(row.Locator("button:has-text('Modifier')")).ToHaveCountAsync(0);
            await Expect(row.Locator("button:has-text('Désactiver')")).ToHaveCountAsync(0);
            await Expect(page.Locator("button:has-text('+ Ajouter un membre')")).ToHaveCountAsync(0);

            // The API refuses too, not only the page.
            var response = await page.APIRequest.PostAsync($"{fixture.BaseUrl}/api/members",
                new() { DataObject = new { firstName = "Hack", lastName = $"ZzzHack{suffix}" } });
            Assert.Equal(403, response.Status);
        }
        finally
        {
            await adminContext.CloseAsync();
            await secretaireContext.CloseAsync();
        }
    }

    [Fact]
    public async Task EcoleDominicale_CanDeactivateAndReactivateAMember()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var moniteur = $"ed{suffix}";
        var lastName = $"ZzzToggle{suffix}";

        var adminContext = await fixture.Browser.NewContextAsync();
        var moniteurContext = await fixture.Browser.NewContextAsync();
        try
        {
            var admin = await adminContext.NewPageAsync();
            await E2EActions.LoginAsync(admin, fixture.BaseUrl, "admin", E2EActions.AdminPassword);
            await E2EActions.CreateMemberAsync(admin, fixture.BaseUrl, "Jean", lastName);
            await E2EActions.CreateUserAsync(admin, fixture.BaseUrl, moniteur, "motdepasse123", "EcoleDominicale");

            var page = await moniteurContext.NewPageAsync();
            await E2EActions.LoginAsync(page, fixture.BaseUrl, moniteur, "motdepasse123");
            await page.FillAsync("input[placeholder='Rechercher par nom ou prénom...']", lastName);
            var row = page.Locator("table tbody tr", new() { HasText = lastName });

            await BlazorInteraction.ClickUntilVisibleAsync(
                page, $"tr:has-text('{lastName}') button:has-text('Désactiver')", row.Locator("text=Inactif"));
            await Expect(row).ToContainTextAsync("Inactif");

            await row.Locator("button:has-text('Réactiver')").ClickAsync();
            await Expect(row).ToContainTextAsync("Actif");
            await Expect(row.Locator("button:has-text('Désactiver')")).ToBeVisibleAsync();
        }
        finally
        {
            await adminContext.CloseAsync();
            await moniteurContext.CloseAsync();
        }
    }

    [Fact]
    public async Task NotLoggedIn_CannotCreateAMemberThroughTheApi()
    {
        var context = await fixture.Browser.NewContextAsync();
        try
        {
            var page = await context.NewPageAsync();
            var response = await page.APIRequest.PostAsync($"{fixture.BaseUrl}/api/members",
                new() { DataObject = new { firstName = "Anonyme", lastName = "Pirate" } });

            Assert.Equal(401, response.Status);
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
