using System.Net;
using System.Net.Http.Json;
using ChurchAttendance.Data;
using ChurchAttendance.Endpoints;
using ChurchAttendance.Models;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

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

    [Fact]
    public async Task ExportCsv_WithoutAuthentication_RedirectsToLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/admin/members/export.csv");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ExportCsv_Authenticated_ReturnsCsvWithMatchingMembers()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Members.AddRange(
                new Member { FirstName = "Alice", LastName = "Martin", Token = "alice-token", CreatedAt = DateTime.UtcNow, IsBaptized = true },
                new Member { FirstName = "Bob", LastName = "Nadeau", Token = "bob-token", CreatedAt = DateTime.UtcNow, IsBaptized = false });
            await db.SaveChangesAsync();
        }
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/admin/members/export.csv");
        var csv = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("﻿Nom,Prenom,Telephone,Naissance,Baptise,Statut", csv);
        Assert.Contains("Martin,Alice", csv);
        Assert.Contains("Nadeau,Bob", csv);
    }

    [Fact]
    public async Task ExportCsv_SearchFilter_OnlyReturnsMatchingMembers()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Members.AddRange(
                new Member { FirstName = "Alice", LastName = "Martin", Token = "alice-token", CreatedAt = DateTime.UtcNow },
                new Member { FirstName = "Bob", LastName = "Nadeau", Token = "bob-token", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/admin/members/export.csv?search=mart");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Contains("Martin,Alice", csv);
        Assert.DoesNotContain("Nadeau,Bob", csv);
    }

    [Fact]
    public async Task ExportCsv_BaptizedFilter_OnlyReturnsMatchingMembers()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Members.AddRange(
                new Member { FirstName = "Alice", LastName = "Martin", Token = "alice-token", CreatedAt = DateTime.UtcNow, IsBaptized = true },
                new Member { FirstName = "Bob", LastName = "Nadeau", Token = "bob-token", CreatedAt = DateTime.UtcNow, IsBaptized = false });
            await db.SaveChangesAsync();
        }
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/admin/members/export.csv?baptized=true");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Contains("Martin,Alice", csv);
        Assert.DoesNotContain("Nadeau,Bob", csv);
    }
}
