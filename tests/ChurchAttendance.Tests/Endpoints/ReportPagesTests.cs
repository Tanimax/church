using System.Net;
using System.Text.RegularExpressions;
using ChurchAttendance.Tests.Infrastructure;

namespace ChurchAttendance.Tests.Endpoints;

// The Sainte Cène and birthday reports, as the server first renders them (with the data
// already loaded), on a clock frozen on Saturday 24 October 2026 in Montréal.
public class ReportPagesTests
{
    private static CustomWebApplicationFactory Factory() =>
        new(new FixedTimeProvider(FixedTimeProvider.SaturdayEveningInMontreal));

    // Each table row's cells, decoded, without the scoped-CSS attributes.
    private static List<List<string>> Rows(string html) =>
        Regex.Matches(html, @"<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline)
            .Select(row => Regex.Matches(row.Groups[1].Value, @"<td[^>]*>(.*?)</td>", RegexOptions.Singleline)
                .Select(cell => WebUtility.HtmlDecode(Regex.Replace(cell.Groups[1].Value, "<[^>]+>", "")).Trim())
                .ToList())
            .Where(cells => cells.Count > 0)
            .ToList();

    [Fact]
    public async Task SainteCene_ListsActiveBaptizedMembers_WithTheMonthsTheyTookIt_ThisYear()
    {
        using var factory = Factory();
        await using (var db = factory.CreateDbContext())
        {
            var ruth = new Member { FirstName = "Ruth", LastName = "Désir", IsBaptized = true, Token = "t-ruth", CreatedAt = DateTime.UtcNow };
            var paul = new Member { FirstName = "Paul", LastName = "Joseph", IsBaptized = true, Token = "t-paul", CreatedAt = DateTime.UtcNow };
            db.Members.AddRange(ruth, paul,
                new Member { FirstName = "Jean", LastName = "NonBaptisé", Token = "t-jean", CreatedAt = DateTime.UtcNow },
                new Member { FirstName = "Anne", LastName = "Partie", IsBaptized = true, IsActive = false, Token = "t-anne", CreatedAt = DateTime.UtcNow });
            foreach (var date in new[] { new DateOnly(2026, 1, 4), new DateOnly(2026, 10, 4), new DateOnly(2025, 12, 7) })
            {
                var session = new ServiceSession { Date = date, Label = "Culte du dimanche" };
                db.ServiceSessions.Add(session);
                db.Attendances.Add(new Attendance { Member = ruth, ServiceSession = session, Type = AttendanceType.SainteCene, CheckedInAt = DateTime.UtcNow });
                // An ordinary attendance is not a Sainte Cène.
                db.Attendances.Add(new Attendance { Member = paul, ServiceSession = session, Type = AttendanceType.Culte, CheckedInAt = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
        }
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var rows = Rows(await client.GetStringAsync("/admin/reports/sainte-cene"));

        // Name, January … December, total; sorted by last name.
        Assert.Equal(2, rows.Count);
        Assert.Equal(["Ruth Désir", "✓", "", "", "", "", "", "", "", "", "✓", "", "", "2"], rows[0]);
        Assert.Equal("Paul Joseph", rows[1][0]);
        Assert.Equal("0", rows[1][^1]);
    }

    [Fact]
    public async Task SainteCene_WithNoBaptizedMember_SaysSo()
    {
        using var factory = Factory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/reports/sainte-cene"));

        Assert.Contains("Aucun membre baptisé actif.", html);
    }

    [Fact]
    public async Task Birthdays_AreThoseOfTheWeekEndingOnTheLastSunday()
    {
        // On Saturday 24 October, the last Sunday is the 18th: the week is Monday 12 to Sunday 18.
        using var factory = Factory();
        await using (var db = factory.CreateDbContext())
        {
            db.Members.AddRange(
                new Member { FirstName = "Ruth", LastName = "Désir", BirthDay = 15, BirthMonth = 10, Token = "t-ruth", CreatedAt = DateTime.UtcNow },
                new Member { FirstName = "Paul", LastName = "Joseph", BirthDay = 20, BirthMonth = 10, Token = "t-paul", CreatedAt = DateTime.UtcNow },
                new Member { FirstName = "Anne", LastName = "Partie", BirthDay = 16, BirthMonth = 10, IsActive = false, Token = "t-anne", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var html = await client.GetStringAsync("/admin/reports/anniversaires");

        Assert.Contains("entre le 12/10/2026 et le 18/10/2026", WebUtility.HtmlDecode(html));
        Assert.Equal([["Jeudi 15 octobre", "Désir", "Ruth"]], Rows(html));
    }

    [Fact]
    public async Task Birthdays_NoneThisWeek_SaysSo()
    {
        using var factory = Factory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/admin/reports/anniversaires"));

        Assert.Contains("Aucun anniversaire cette semaine.", html);
    }
}
