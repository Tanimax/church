using System.Net;
using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ChurchAttendance.Tests.Endpoints;

public class ReportsEndpointsTests
{
    // Program.cs falls back to this password in the Development environment
    // (which CustomWebApplicationFactory always uses) when ADMIN_PASSWORD isn't set.
    private const string DevAdminPassword = "dev-only-password";

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(CustomWebApplicationFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["password"] = DevAdminPassword
        });
        var loginResponse = await client.PostAsync("/admin/login", loginForm);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/admin/members", loginResponse.Headers.Location?.ToString());
        return client;
    }

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
        var client = await CreateAuthenticatedClientAsync(factory);

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
        var today = DateOnly.FromDateTime(DateTime.Today);

        await using (var db = factory.CreateDbContext())
        {
            var member = new Member { FirstName = "Alice", LastName = "Martin", Token = "alice-token", CreatedAt = DateTime.UtcNow };
            var session = new ServiceSession { Date = today, Label = "Culte du dimanche" };
            db.Members.Add(member);
            db.ServiceSessions.Add(session);
            await db.SaveChangesAsync();

            var earliest = new DateTime(today.Year, today.Month, today.Day, 9, 0, 0, DateTimeKind.Utc);
            db.Attendances.AddRange(
                new Attendance { MemberId = member.Id, ServiceSessionId = session.Id, Type = AttendanceType.Culte, CheckedInAt = earliest },
                new Attendance { MemberId = member.Id, ServiceSessionId = session.Id, Type = AttendanceType.SainteCene, CheckedInAt = earliest.AddMinutes(5) });
            await db.SaveChangesAsync();
        }

        var client = await CreateAuthenticatedClientAsync(factory);
        var response = await client.GetAsync("/admin/reports/export.csv");
        var csv = await response.Content.ReadAsStringAsync();
        var dataLines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();

        Assert.Single(dataLines);
        Assert.Contains("Alice Martin", dataLines[0]);
        Assert.Contains("09:00", dataLines[0]);
    }
}
