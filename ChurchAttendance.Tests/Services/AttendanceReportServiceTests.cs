using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services;
using ChurchAttendance.Tests.Infrastructure;

namespace ChurchAttendance.Tests.Services;

public class AttendanceReportServiceTests
{
    private static readonly DateOnly Sunday = new(2026, 10, 4);
    private static readonly DateOnly PreviousSunday = Sunday.AddDays(-7);

    private static async Task<Member> AddMemberAsync(AppDbContext db, string firstName, string lastName, bool isActive = true)
    {
        var member = new Member
        {
            FirstName = firstName,
            LastName = lastName,
            Token = Guid.NewGuid().ToString("N"),
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    private static async Task<ServiceSession> AddSessionAsync(AppDbContext db, DateOnly date)
    {
        var session = new ServiceSession { Date = date, Label = "Culte du dimanche" };
        db.ServiceSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private static DateTime At(DateOnly date, int hour, int minute) =>
        new(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPresencesAsync_CulteAndSainteCeneSameSunday_CountsOncePerMemberWithEarliestCheckIn()
    {
        using var factory = new CustomWebApplicationFactory();
        await using var db = factory.CreateDbContext();
        var alice = await AddMemberAsync(db, "Alice", "Martin");
        var session = await AddSessionAsync(db, Sunday);
        db.Attendances.AddRange(
            new Attendance { MemberId = alice.Id, ServiceSessionId = session.Id, Type = AttendanceType.SainteCene, CheckedInAt = At(Sunday, 10, 15) },
            new Attendance { MemberId = alice.Id, ServiceSessionId = session.Id, Type = AttendanceType.Culte, CheckedInAt = At(Sunday, 9, 50) });
        await db.SaveChangesAsync();

        var presences = await AttendanceReportService.GetPresencesAsync(db, Sunday, Sunday);

        var presence = Assert.Single(presences);
        Assert.Equal("Alice Martin", presence.FullName);
        Assert.Equal(At(Sunday, 9, 50), presence.CheckedInAt);
    }

    [Fact]
    public async Task GetPresencesAsync_RangeKeepsOneRowPerSunday_NewestFirstThenByName()
    {
        using var factory = new CustomWebApplicationFactory();
        await using var db = factory.CreateDbContext();
        var zoe = await AddMemberAsync(db, "Zoé", "Bernard");
        var alice = await AddMemberAsync(db, "Alice", "Martin");
        var earlier = await AddSessionAsync(db, PreviousSunday);
        var later = await AddSessionAsync(db, Sunday);
        db.Attendances.AddRange(
            new Attendance { MemberId = zoe.Id, ServiceSessionId = earlier.Id, CheckedInAt = At(PreviousSunday, 10, 0) },
            new Attendance { MemberId = zoe.Id, ServiceSessionId = later.Id, CheckedInAt = At(Sunday, 10, 0) },
            new Attendance { MemberId = alice.Id, ServiceSessionId = later.Id, CheckedInAt = At(Sunday, 10, 5) });
        await db.SaveChangesAsync();

        var presences = await AttendanceReportService.GetPresencesAsync(db, PreviousSunday, Sunday);

        Assert.Equal(
            [(Sunday, "Alice Martin"), (Sunday, "Zoé Bernard"), (PreviousSunday, "Zoé Bernard")],
            presences.Select(p => (p.Date, p.FullName)));
    }

    [Fact]
    public async Task GetAbsentMembersAsync_ExcludesPresentAndInactive_LongestAbsentFirst()
    {
        using var factory = new CustomWebApplicationFactory();
        await using var db = factory.CreateDbContext();
        var present = await AddMemberAsync(db, "Paul", "Present");
        var recent = await AddMemberAsync(db, "Rita", "Recente");
        await AddMemberAsync(db, "Nadia", "Jamais");
        await AddMemberAsync(db, "Ivan", "Inactif", isActive: false);
        var earlier = await AddSessionAsync(db, PreviousSunday);
        var session = await AddSessionAsync(db, Sunday);
        db.Attendances.AddRange(
            new Attendance { MemberId = recent.Id, ServiceSessionId = earlier.Id, CheckedInAt = At(PreviousSunday, 10, 0) },
            new Attendance { MemberId = present.Id, ServiceSessionId = session.Id, CheckedInAt = At(Sunday, 10, 0) });
        await db.SaveChangesAsync();

        var absents = await AttendanceReportService.GetAbsentMembersAsync(db, [present.Id]);

        Assert.Equal(["Nadia Jamais", "Rita Recente"], absents.Select(a => a.FullName));
        Assert.Null(absents[0].LastAttendance);
        Assert.Equal(PreviousSunday, absents[1].LastAttendance);
    }
}
