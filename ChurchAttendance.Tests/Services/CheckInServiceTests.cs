using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Tests.Services;

public class CheckInServiceTests
{
    private static async Task<Member> SeedMemberAsync(AppDbContext db, bool isBaptized = true)
    {
        var member = new Member
        {
            FirstName = "Jean",
            LastName = "Dupont",
            Token = Guid.NewGuid().ToString("N"),
            IsActive = true,
            IsBaptized = isBaptized,
            CreatedAt = DateTime.UtcNow
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    [Fact]
    public async Task CheckInMemberAsync_SainteCene_NonBaptizedMember_ReturnsNotBaptizedAndRecordsNothing()
    {
        using var factory = new CustomWebApplicationFactory();
        await using var db = factory.CreateDbContext();
        var member = await SeedMemberAsync(db, isBaptized: false);

        var response = await CheckInService.CheckInMemberAsync(db, member, AttendanceType.SainteCene);

        Assert.Equal("not_baptized", response.Status);
        Assert.Equal("Jean Dupont", response.FullName);
        Assert.Null(response.CheckedInAt);
        Assert.Equal(0, await db.Attendances.CountAsync());
    }

    [Fact]
    public async Task CheckInMemberAsync_SainteCene_RecordsAttendanceAndAutoRecordsCulte()
    {
        using var factory = new CustomWebApplicationFactory();
        await using var db = factory.CreateDbContext();
        var member = await SeedMemberAsync(db);

        var response = await CheckInService.CheckInMemberAsync(db, member, AttendanceType.SainteCene);

        Assert.Equal("ok", response.Status);
        Assert.NotNull(response.CheckedInAt);

        var attendances = await db.Attendances.ToListAsync();
        Assert.Equal(2, attendances.Count);
        Assert.Contains(attendances, a => a.Type == AttendanceType.SainteCene && a.CheckedInBy == null);
        Assert.Contains(attendances, a => a.Type == AttendanceType.Culte && a.CheckedInBy == "Auto (Sainte Cène)");
    }

    [Fact]
    public async Task CheckInMemberAsync_SecondCallSameDay_ReturnsDuplicate()
    {
        using var factory = new CustomWebApplicationFactory();
        await using var db = factory.CreateDbContext();
        var member = await SeedMemberAsync(db);

        var first = await CheckInService.CheckInMemberAsync(db, member, AttendanceType.SainteCene);
        var second = await CheckInService.CheckInMemberAsync(db, member, AttendanceType.SainteCene);

        Assert.Equal("ok", first.Status);
        Assert.Equal("duplicate", second.Status);
        Assert.Equal(first.CheckedInAt, second.CheckedInAt);

        var sainteCeneCount = await db.Attendances.CountAsync(a => a.Type == AttendanceType.SainteCene);
        Assert.Equal(1, sainteCeneCount);
    }
}
