using System.Net.Http.Json;
using ChurchAttendance.Data;
using ChurchAttendance.Endpoints;
using ChurchAttendance.Models;
using ChurchAttendance.Services;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Tests.Endpoints;

public class CheckInEndpointsTests
{
    private static async Task<Member> SeedMemberAsync(
        AppDbContext db, string token = "member-token", bool isActive = true, bool isBaptized = true)
    {
        var member = new Member
        {
            FirstName = "Jean",
            LastName = "Dupont",
            Token = token,
            IsActive = isActive,
            IsBaptized = isBaptized,
            CreatedAt = DateTime.UtcNow
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    [Fact]
    public async Task CheckIn_UnknownToken_ReturnsNotFoundStatus()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("does-not-exist"));
        var body = await response.Content.ReadFromJsonAsync<CheckInResponse>();

        response.EnsureSuccessStatusCode();
        Assert.Equal("not_found", body!.Status);
    }

    [Fact]
    public async Task CheckIn_InactiveMember_ReturnsNotFoundStatus()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db, isActive: false);
        }
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("member-token"));
        var body = await response.Content.ReadFromJsonAsync<CheckInResponse>();

        Assert.Equal("not_found", body!.Status);
    }

    [Fact]
    public async Task CheckIn_ValidToken_RecordsAttendanceAndReturnsOk()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db);
        }
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("member-token"));
        var body = await response.Content.ReadFromJsonAsync<CheckInResponse>();

        Assert.Equal("ok", body!.Status);
        Assert.Equal("Jean Dupont", body.FullName);
        Assert.NotNull(body.CheckedInAt);

        await using var verifyDb = factory.CreateDbContext();
        Assert.Equal(1, await verifyDb.Attendances.CountAsync());
    }

    [Fact]
    public async Task CheckIn_SecondCheckInSameDay_ReturnsDuplicateStatus()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db);
        }
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("member-token"));
        var second = await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("member-token"));
        var body = await second.Content.ReadFromJsonAsync<CheckInResponse>();

        Assert.Equal("duplicate", body!.Status);

        await using var verifyDb = factory.CreateDbContext();
        Assert.Equal(1, await verifyDb.Attendances.CountAsync());
    }

    [Fact]
    public async Task CheckIn_SainteCene_AlsoAutoRecordsCulteAttendance()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db);
        }
        var client = factory.CreateClient();

        using var content = new StringContent(
            """{"token":"member-token","type":"SainteCene"}""",
            System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/checkin", content);
        var body = await response.Content.ReadFromJsonAsync<CheckInResponse>();

        Assert.Equal("ok", body!.Status);

        await using var verifyDb = factory.CreateDbContext();
        var attendances = await verifyDb.Attendances.ToListAsync();
        Assert.Equal(2, attendances.Count);
        Assert.Contains(attendances, a => a.Type == AttendanceType.SainteCene && a.CheckedInBy == null);
        Assert.Contains(attendances, a => a.Type == AttendanceType.Culte && a.CheckedInBy == "Auto (Sainte Cène)");
    }

    [Fact]
    public async Task CheckIn_SainteCene_NonBaptizedMember_ReturnsNotBaptizedStatus()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db, isBaptized: false);
        }
        var client = factory.CreateClient();

        using var content = new StringContent(
            """{"token":"member-token","type":"SainteCene"}""",
            System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/checkin", content);
        var body = await response.Content.ReadFromJsonAsync<CheckInResponse>();

        Assert.Equal("not_baptized", body!.Status);
        Assert.Equal("Jean Dupont", body.FullName);

        await using var verifyDb = factory.CreateDbContext();
        Assert.Equal(0, await verifyDb.Attendances.CountAsync());
    }

    [Fact]
    public async Task CheckIn_Culte_NonBaptizedMember_StillAllowed()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db, isBaptized: false);
        }
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("member-token"));
        var body = await response.Content.ReadFromJsonAsync<CheckInResponse>();

        Assert.Equal("ok", body!.Status);
    }

    [Fact]
    public async Task CheckIn_EcoleDominicale_NonBaptizedMember_StillAllowedAndDoesNotAutoRecordCulte()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db, isBaptized: false);
        }
        var client = factory.CreateClient();

        using var content = new StringContent(
            """{"token":"member-token","type":"EcoleDominicale"}""",
            System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/checkin", content);
        var body = await response.Content.ReadFromJsonAsync<CheckInResponse>();

        Assert.Equal("ok", body!.Status);

        await using var verifyDb = factory.CreateDbContext();
        var attendances = await verifyDb.Attendances.ToListAsync();
        Assert.Single(attendances);
        Assert.Equal(AttendanceType.EcoleDominicale, attendances[0].Type);
    }

    [Fact]
    public async Task CheckIn_SainteCeneAlreadyScannedByOneUser_SecondUserGetsDuplicateAndCardIsNotDoubleCounted()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db);
            db.Users.Add(new User
            {
                Username = "secretaire1",
                PasswordHash = UserService.HashPassword("secretaire-password"),
                Role = UserRole.Secretaire,
                CreatedAt = DateTime.UtcNow
            });
            db.Users.Add(new User
            {
                Username = "ecoledom1",
                PasswordHash = UserService.HashPassword("ecoledom-password"),
                Role = UserRole.EcoleDominicale,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Two different logged-in users, e.g. two scanners running at the same Sainte Cène
        // service — one already checked this card in, so the other must not be able to
        // record it again for that same session.
        var firstUserClient = await TestAuth.CreateAuthenticatedClientAsync(factory, "secretaire1", "secretaire-password");
        var secondUserClient = await TestAuth.CreateAuthenticatedClientAsync(factory, "ecoledom1", "ecoledom-password");

        using var firstScan = new StringContent(
            """{"token":"member-token","type":"SainteCene"}""",
            System.Text.Encoding.UTF8, "application/json");
        var firstResponse = await firstUserClient.PostAsync("/api/checkin", firstScan);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<CheckInResponse>();
        Assert.Equal("ok", firstBody!.Status);

        using var secondScan = new StringContent(
            """{"token":"member-token","type":"SainteCene"}""",
            System.Text.Encoding.UTF8, "application/json");
        var secondResponse = await secondUserClient.PostAsync("/api/checkin", secondScan);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<CheckInResponse>();
        Assert.Equal("duplicate", secondBody!.Status);
        Assert.Equal(firstBody.CheckedInAt, secondBody.CheckedInAt);

        await using var verifyDb = factory.CreateDbContext();
        var sainteCeneCount = await verifyDb.Attendances.CountAsync(a => a.Type == AttendanceType.SainteCene);
        Assert.Equal(1, sainteCeneCount);
    }

    [Fact]
    public async Task CheckIn_SainteCeneAfterCulteAlreadyRecorded_DoesNotDuplicateCulte()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db);
        }
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("member-token"));
        using var content = new StringContent(
            """{"token":"member-token","type":"SainteCene"}""",
            System.Text.Encoding.UTF8, "application/json");
        await client.PostAsync("/api/checkin", content);

        await using var verifyDb = factory.CreateDbContext();
        var culteCount = await verifyDb.Attendances.CountAsync(a => a.Type == AttendanceType.Culte);
        Assert.Equal(1, culteCount);
    }
}
