using System.Net;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Tests.Endpoints;

// The visitor form (/visitor) is public by nature: a first-time visitor scans the QR code at
// the entrance and leaves their name and phone number, without an account.
public class VisitorFormTests
{
    private static HttpClient Anonymous(CustomWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static FormUrlEncodedContent Form(string fullName, string phone, string email = "") =>
        new(new Dictionary<string, string> { ["fullName"] = fullName, ["phone"] = phone, ["email"] = email });

    [Fact]
    public async Task Form_IsOpenToAnyone()
    {
        using var factory = new CustomWebApplicationFactory();

        var html = await Anonymous(factory).GetStringAsync("/visitor");

        Assert.Contains("<form method=\"post\" action=\"/api/visitors\">", html);
        Assert.DoesNotContain("class=\"error\"", html);
    }

    [Fact]
    public async Task Submit_SavesTheVisitor_DatedToday_AndThanksThem()
    {
        // Saturday evening in Montréal is already Sunday in UTC: the visit is dated in church time.
        using var factory = new CustomWebApplicationFactory(new FixedTimeProvider(FixedTimeProvider.SaturdayEveningInMontreal));
        var client = Anonymous(factory);

        var response = await client.PostAsync("/api/visitors", Form("  Ruth Désir ", " 514-555-0199 ", "  "));
        var thanks = await client.GetStringAsync(response.Headers.Location!.OriginalString);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/visitor?status=success", response.Headers.Location.OriginalString);
        Assert.Contains("Merci de votre visite", thanks);
        await using var db = factory.CreateDbContext();
        var visitor = await db.Visitors.SingleAsync();
        Assert.Equal("Ruth Désir", visitor.FullName);
        Assert.Equal("514-555-0199", visitor.Phone);
        Assert.Null(visitor.Email);
        Assert.Equal(new DateOnly(2026, 10, 24), visitor.VisitDate);
    }

    [Fact]
    public async Task Submit_KeepsTheEmail_WhenGiven()
    {
        using var factory = new CustomWebApplicationFactory();

        await Anonymous(factory).PostAsync("/api/visitors", Form("Ruth Désir", "514-555-0199", " ruth@exemple.ca "));

        await using var db = factory.CreateDbContext();
        Assert.Equal("ruth@exemple.ca", (await db.Visitors.SingleAsync()).Email);
    }

    [Theory]
    [InlineData("", "514-555-0199")]
    [InlineData("Ruth Désir", "   ")]
    public async Task Submit_WithoutNameOrPhone_SavesNothing_AndShowsTheError(string fullName, string phone)
    {
        using var factory = new CustomWebApplicationFactory();
        var client = Anonymous(factory);

        var response = await client.PostAsync("/api/visitors", Form(fullName, phone));
        var form = await client.GetStringAsync(response.Headers.Location!.OriginalString);

        Assert.Equal("/visitor?status=error", response.Headers.Location.OriginalString);
        Assert.Contains("Le nom complet et le téléphone sont requis.", form);
        await using var db = factory.CreateDbContext();
        Assert.Empty(db.Visitors);
    }

    [Fact]
    public async Task QrCode_ForTheEntrance_IsAPng()
    {
        using var factory = new CustomWebApplicationFactory();

        var response = await Anonymous(factory).GetAsync("/visitor/qr.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], (await response.Content.ReadAsByteArrayAsync())[..4]);
    }
}
