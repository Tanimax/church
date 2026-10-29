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

    [Fact]
    public async Task Pdf_ForTheSecretariat_IsTheSavedQuarter()
    {
        using var factory = new CustomWebApplicationFactory();
        await factory.SeedUserAsync("secretaire", "motdepasse123", UserRole.Secretaire);
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "secretaire", "motdepasse123");

        var response = await client.GetAsync("/admin/programmation/2026/4/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Service_dominical_du_4_octobre_au_27_décembre_2026.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await response.Content.ReadAsByteArrayAsync())[..4]));
    }

    [Fact]
    public async Task Pdf_EcoleDominicale_IsSentAway()
    {
        using var factory = new CustomWebApplicationFactory();
        await factory.SeedUserAsync("moniteur", "motdepasse123", UserRole.EcoleDominicale);
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "moniteur", "motdepasse123");

        var response = await client.GetAsync("/admin/programmation/2026/4/pdf");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Pdf_NotLoggedIn_IsSentToLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/admin/programmation/2026/4/pdf");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/login", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/admin/programmation/2026/5/pdf")]
    [InlineData("/admin/programmation/1999/1/pdf")]
    public async Task Pdf_NoSuchQuarter_IsNotFound(string url)
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
