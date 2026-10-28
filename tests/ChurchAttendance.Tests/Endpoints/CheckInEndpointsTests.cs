using System.Net;
using System.Net.Http.Json;
using ChurchAttendance.Infrastructure.Persistence;
using ChurchAttendance.Application.CheckIn;
using ChurchAttendance.Features.CheckIn;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

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
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

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
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

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
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

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
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

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
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

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
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

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
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

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
            await factory.SeedUserAsync("secretaire1", "secretaire-password", UserRole.Secretaire);
            await factory.SeedUserAsync("ecoledom1", "ecoledom-password", UserRole.EcoleDominicale);
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
        Assert.NotNull(secondBody.CheckedInAt);

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
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("member-token"));
        using var content = new StringContent(
            """{"token":"member-token","type":"SainteCene"}""",
            System.Text.Encoding.UTF8, "application/json");
        await client.PostAsync("/api/checkin", content);

        await using var verifyDb = factory.CreateDbContext();
        var culteCount = await verifyDb.Attendances.CountAsync(a => a.Type == AttendanceType.Culte);
        Assert.Equal(1, culteCount);
    }

    [Fact]
    public async Task CheckIn_SaturdayEveningInMontreal_CreatesSaturdaySessionNotUtcSunday()
    {
        using var factory = new CustomWebApplicationFactory(new FixedTimeProvider(FixedTimeProvider.SaturdayEveningInMontreal));
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db);
        }
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("member-token"));
        var body = await response.Content.ReadFromJsonAsync<CheckInResponse>();

        Assert.Equal("ok", body!.Status);
        await using var verifyDb = factory.CreateDbContext();
        var session = await verifyDb.ServiceSessions.SingleAsync();
        Assert.Equal(new DateOnly(2026, 10, 24), session.Date);
    }

    [Fact]
    public async Task CheckIn_NotLoggedIn_IsRejectedWithUnauthorized()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db);
        }
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("member-token", AttendanceType.SainteCene));

        // A JSON API answers 401 rather than redirecting to the HTML login page.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var verifyDb = factory.CreateDbContext();
        Assert.Equal(0, await verifyDb.Attendances.CountAsync());
    }

    [Fact]
    public async Task CheckIn_FirstScanOfTheDayFromTwoScannersAtOnce_BothSucceedWithOneSession()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db, token: "card-1");
            await SeedMemberAsync(db, token: "card-2");
        }
        var scanner1 = await TestAuth.CreateAuthenticatedClientAsync(factory);
        var scanner2 = await TestAuth.CreateAuthenticatedClientAsync(factory);

        // Both requests find no session for today and try to create it; the unique index on
        // the date rejects one of them. Repeated because the race doesn't happen every time.
        for (var round = 0; round < 20; round++)
        {
            await using (var db = factory.CreateDbContext())
            {
                await db.Attendances.ExecuteDeleteAsync();
                await db.ServiceSessions.ExecuteDeleteAsync();
            }

            var responses = await Task.WhenAll(
                scanner1.PostAsJsonAsync("/api/checkin", new CheckInRequest("card-1", AttendanceType.SainteCene)),
                scanner2.PostAsJsonAsync("/api/checkin", new CheckInRequest("card-2", AttendanceType.SainteCene)));

            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("ok", (await response.Content.ReadFromJsonAsync<CheckInResponse>())!.Status);
            }
            await using var verifyDb = factory.CreateDbContext();
            Assert.Equal(1, await verifyDb.ServiceSessions.CountAsync());
            Assert.Equal(2, await verifyDb.Attendances.CountAsync(a => a.Type == AttendanceType.SainteCene));
            Assert.Equal(2, await verifyDb.Attendances.CountAsync(a => a.Type == AttendanceType.Culte));
        }
    }

    [Fact]
    public async Task CheckIn_SameCardFromTwoScannersAtOnce_OneOkOneDuplicate_AndCulteStillRecorded()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            await SeedMemberAsync(db);
            // Today's session already exists, so only the attendance insert races here.
            var today = factory.Services.GetRequiredService<ChurchClock>().Today;
            db.ServiceSessions.Add(new ServiceSession { Date = today, Label = "Culte du dimanche" });
            await db.SaveChangesAsync();
        }
        var scanner1 = await TestAuth.CreateAuthenticatedClientAsync(factory);
        var scanner2 = await TestAuth.CreateAuthenticatedClientAsync(factory);

        for (var round = 0; round < 20; round++)
        {
            await using (var db = factory.CreateDbContext())
            {
                await db.Attendances.ExecuteDeleteAsync();
            }

            var responses = await Task.WhenAll(
                scanner1.PostAsJsonAsync("/api/checkin", new CheckInRequest("member-token", AttendanceType.SainteCene)),
                scanner2.PostAsJsonAsync("/api/checkin", new CheckInRequest("member-token", AttendanceType.SainteCene)));

            var statuses = new List<string>();
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                statuses.Add((await response.Content.ReadFromJsonAsync<CheckInResponse>())!.Status);
            }
            Assert.Equal(["duplicate", "ok"], statuses.Order());
            await using var verifyDb = factory.CreateDbContext();
            Assert.Equal(1, await verifyDb.Attendances.CountAsync(a => a.Type == AttendanceType.SainteCene));
            Assert.Equal(1, await verifyDb.Attendances.CountAsync(a => a.Type == AttendanceType.Culte));
        }
    }
}
