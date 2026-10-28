using System.Net;
using ChurchAttendance.Infrastructure.Persistence;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Tests.Endpoints;

public class ReportsEndpointsTests
{
    [Fact]
    public async Task ExportCsv_WithoutAuthentication_RedirectsToLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/admin/reports/export.csv");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ExportCsv_Authenticated_ReturnsCsvWithHeaderRow()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/admin/reports/export.csv");
        var csv = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("Date,Nom,HeureArrivee", csv);
    }

    [Fact]
    public async Task ExportCsv_MemberWithCulteAndSainteCeneSameSession_CollapsesToSingleRowUsingEarliestCheckIn()
    {
        using var factory = new CustomWebApplicationFactory();
        var clock = factory.Services.GetRequiredService<ChurchClock>();
        var today = clock.Today;

        await using (var db = factory.CreateDbContext())
        {
            var member = new Member { FirstName = "Alice", LastName = "Martin", Token = "alice-token", CreatedAt = DateTime.UtcNow };
            var session = new ServiceSession { Date = today, Label = "Culte du dimanche" };
            db.Members.Add(member);
            db.ServiceSessions.Add(session);
            await db.SaveChangesAsync();

            // 09:00 church-local time, stored as UTC like real check-ins; the export must show 09:00.
            var earliest = clock.ToUtc(today.ToDateTime(new TimeOnly(9, 0)));
            db.Attendances.AddRange(
                new Attendance { MemberId = member.Id, ServiceSessionId = session.Id, Type = AttendanceType.Culte, CheckedInAt = earliest },
                new Attendance { MemberId = member.Id, ServiceSessionId = session.Id, Type = AttendanceType.SainteCene, CheckedInAt = earliest.AddMinutes(5) });
            await db.SaveChangesAsync();
        }

        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);
        var response = await client.GetAsync("/admin/reports/export.csv");
        var csv = await response.Content.ReadAsStringAsync();
        var dataLines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();

        Assert.Single(dataLines);
        Assert.Contains("Alice Martin", dataLines[0]);
        Assert.Contains("09:00", dataLines[0]);
    }
}
