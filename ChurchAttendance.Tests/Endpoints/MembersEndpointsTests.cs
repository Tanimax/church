using System.IO.Compression;
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
    public async Task ExportCsv_EmptySearchAndBaptizedQueryValues_ReturnsAllMembers()
    {
        // Members.razor's "Exporter en Excel" link always includes all three query params, even
        // when no filter is selected (search=&baptized=&active=) — this must not 400/crash.
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Members.Add(new Member { FirstName = "Alice", LastName = "Martin", Token = "alice-token", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/admin/members/export.csv?search=&baptized=&active=");
        var csv = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Martin,Alice", csv);
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

    [Fact]
    public async Task ExportCsv_ActiveFilter_OnlyReturnsMatchingMembers()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Members.AddRange(
                new Member { FirstName = "Alice", LastName = "Martin", Token = "alice-token", CreatedAt = DateTime.UtcNow, IsActive = true },
                new Member { FirstName = "Bob", LastName = "Nadeau", Token = "bob-token", CreatedAt = DateTime.UtcNow, IsActive = false });
            await db.SaveChangesAsync();
        }
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/admin/members/export.csv?active=false");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Contains("Nadeau,Bob", csv);
        Assert.DoesNotContain("Martin,Alice", csv);
    }

    [Fact]
    public async Task GenerateCards_WithoutAuthentication_RedirectsToLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/admin/members/cards.zip?ids=1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task GenerateCards_NoIdsSelected_ReturnsBadRequest()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/admin/members/cards.zip");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateCards_SelectedMembers_ReturnsZipWithOnePngPerMember()
    {
        using var factory = new CustomWebApplicationFactory();
        int aliceId, bobId;
        await using (var db = factory.CreateDbContext())
        {
            var alice = new Member { FirstName = "Alice", LastName = "Martin", Token = "alice-token", CreatedAt = DateTime.UtcNow };
            var bob = new Member { FirstName = "Bob", LastName = "Nadeau", Token = "bob-token", CreatedAt = DateTime.UtcNow };
            db.Members.AddRange(alice, bob);
            await db.SaveChangesAsync();
            aliceId = alice.Id;
            bobId = bob.Id;
        }
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync($"/admin/members/cards.zip?ids={aliceId}&ids={bobId}");
        var zipBytes = await response.Content.ReadAsByteArrayAsync();

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);

        using var zip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        Assert.Equal(2, zip.Entries.Count);
        Assert.Contains(zip.Entries, e => e.Name == "Alice Martin.png");
        Assert.Contains(zip.Entries, e => e.Name == "Bob Nadeau.png");

        await using var pngStream = zip.GetEntry("Alice Martin.png")!.Open();
        using var pngBytes = new MemoryStream();
        await pngStream.CopyToAsync(pngBytes);
        Assert.Equal(PngSignature, pngBytes.ToArray()[..PngSignature.Length]);
    }

    [Fact]
    public async Task GenerateCards_MembersWithSameName_DisambiguatesFileNames()
    {
        using var factory = new CustomWebApplicationFactory();
        int firstId, secondId;
        await using (var db = factory.CreateDbContext())
        {
            var first = new Member { FirstName = "Jean", LastName = "Dupont", Token = "jean-1", CreatedAt = DateTime.UtcNow };
            var second = new Member { FirstName = "Jean", LastName = "Dupont", Token = "jean-2", CreatedAt = DateTime.UtcNow };
            db.Members.AddRange(first, second);
            await db.SaveChangesAsync();
            firstId = first.Id;
            secondId = second.Id;
        }
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync($"/admin/members/cards.zip?ids={firstId}&ids={secondId}");
        var zipBytes = await response.Content.ReadAsByteArrayAsync();

        using var zip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        Assert.Contains(zip.Entries, e => e.Name == "Jean Dupont.png");
        Assert.Contains(zip.Entries, e => e.Name == "Jean Dupont (2).png");
    }
}
