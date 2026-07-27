using System.Net;
using System.Net.Http.Json;
using ChurchAttendance.Data;
using ChurchAttendance.Endpoints;
using ChurchAttendance.Models;
using ChurchAttendance.Tests.Infrastructure;

namespace ChurchAttendance.Tests.Endpoints;

public class MembersEndpointsTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task CreateMember_ValidRequest_ReturnsCreatedWithTokenAndCardUrl()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/members/", new CreateMemberRequest("Jean", "Dupont", "555-1234"));
        var body = await response.Content.ReadFromJsonAsync<MemberResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(body!.Id > 0);
        Assert.Equal("Jean", body.FirstName);
        Assert.Equal("Dupont", body.LastName);
        Assert.Equal("Jean Dupont", body.FullName);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.EndsWith($"/card/{body.Token}", body.CardUrl);
    }

    [Fact]
    public async Task CreateMember_MissingLastName_ReturnsBadRequest()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/members/", new CreateMemberRequest("Jean", "   ", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetQrPng_ValidToken_ReturnsPngImage()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/members/", new CreateMemberRequest("Jean", "Dupont", null));
        var member = await created.Content.ReadFromJsonAsync<MemberResponse>();

        var qrResponse = await client.GetAsync($"/api/members/{member!.Token}/qr.png");
        var bytes = await qrResponse.Content.ReadAsByteArrayAsync();

        qrResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/png", qrResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngSignature, bytes[..PngSignature.Length]);
    }

    [Fact]
    public async Task GetQrPng_UnknownToken_ReturnsNotFound()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/members/does-not-exist/qr.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetQrPng_InactiveMember_ReturnsNotFound()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Members.Add(new Member
            {
                FirstName = "Inactive",
                LastName = "Member",
                Token = "inactive-token",
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/members/inactive-token/qr.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
