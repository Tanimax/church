using System.Net;
using ChurchAttendance.Tests.Infrastructure;

namespace ChurchAttendance.Tests.Endpoints;

// The Programmation page is for whoever prepares what the church communicates
// (AuthorizationPolicies.ManageCommunication: Admin and Secrétaire).
public class ProgrammationPageTests
{
    [Theory]
    [InlineData(UserRole.Secretaire)]
    [InlineData(UserRole.Admin)]
    public async Task AllowedRoles_SeeThePage(UserRole role)
    {
        using var factory = new CustomWebApplicationFactory();
        await factory.SeedUserAsync("compte", "motdepasse123", role);
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "compte", "motdepasse123");

        var response = await client.GetAsync("/admin/programmation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Programmation des services", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EcoleDominicale_IsSentAway()
    {
        using var factory = new CustomWebApplicationFactory();
        await factory.SeedUserAsync("moniteur", "motdepasse123", UserRole.EcoleDominicale);
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "moniteur", "motdepasse123");

        var response = await client.GetAsync("/admin/programmation");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/admin/login", response.Headers.Location?.OriginalString.Replace("http://localhost", ""));
    }
}
