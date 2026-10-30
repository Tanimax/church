using System.Net;
using ChurchAttendance.Application.Announcements;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Tests.Endpoints;

// The Annonces page is for whoever prepares what the church communicates
// (AuthorizationPolicies.ManageCommunication: Admin and Secrétaire).
public class AnnoncesPageTests
{
    [Theory]
    [InlineData(UserRole.Secretaire)]
    [InlineData(UserRole.Admin)]
    public async Task AllowedRoles_SeeTheAnnouncements_AndTheUpcomingSundaysTheme(UserRole role)
    {
        // Saturday 24 October: the announcements are prepared for Sunday the 25th, in October.
        using var factory = new CustomWebApplicationFactory(new FixedTimeProvider(FixedTimeProvider.SaturdayEveningInMontreal));
        await factory.SeedUserAsync("compte", "motdepasse123", role);
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<AnnouncementService>();
            await service.SaveThemeAsync(2026, 10, "Octobre : Mois d'actions de grâces");
            await service.SaveAsync(new AnnouncementFormModel
            {
                Kind = AnnouncementKind.Event, EventDate = new DateOnly(2026, 10, 31), EventTime = "à 18h00",
                Title = "Conférence du ministère des hommes et celui des dames.",
            });
        }
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "compte", "motdepasse123");

        var response = await client.GetAsync("/admin/annonces");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Samedi 31 octobre 2026, à 18h00 :", html);
        Assert.Contains("Conférence du ministère des hommes et celui des dames.", html);
        Assert.Contains("value=\"Octobre : Mois d'actions de grâces\"", html);
        Assert.Contains("Annonces du dimanche 25/10/2026 :", html);
    }

    [Fact]
    public async Task EcoleDominicale_IsSentAway()
    {
        using var factory = new CustomWebApplicationFactory();
        await factory.SeedUserAsync("moniteur", "motdepasse123", UserRole.EcoleDominicale);
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "moniteur", "motdepasse123");

        var response = await client.GetAsync("/admin/annonces");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/admin/login", response.Headers.Location?.OriginalString.Replace("http://localhost", ""));
    }

    [Fact]
    public async Task PowerPoint_ForTheSecretariat_IsThatSundaysDeck()
    {
        using var factory = new CustomWebApplicationFactory();
        await factory.SeedUserAsync("secretaire", "motdepasse123", UserRole.Secretaire);
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "secretaire", "motdepasse123");

        var response = await client.GetAsync("/admin/annonces/2026-11-01/pptx");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Features.Announcements.AnnouncementEndpoints.PowerPointContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Annonces_du_dimanche_1er_novembre_2026.pptx", response.Content.Headers.ContentDisposition?.FileNameStar);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("PK"u8.ToArray(), bytes[..2]); // a PowerPoint file is a zip
    }

    [Theory]
    [InlineData("/admin/annonces/2026-10-31/pptx")] // a Saturday
    [InlineData("/admin/annonces/31-10-2026/pptx")]
    [InlineData("/admin/annonces/demain/pptx")]
    public async Task PowerPoint_ForADayThatIsNotASunday_IsNotFound(string url)
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task PowerPoint_EcoleDominicale_IsSentAway()
    {
        using var factory = new CustomWebApplicationFactory();
        await factory.SeedUserAsync("moniteur", "motdepasse123", UserRole.EcoleDominicale);
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "moniteur", "motdepasse123");

        var response = await client.GetAsync("/admin/annonces/2026-11-01/pptx");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
