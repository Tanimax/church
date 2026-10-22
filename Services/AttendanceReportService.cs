using ChurchAttendance.Data;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Services;

public record SundayPresence(int MemberId, string FullName, DateOnly Date, DateTime CheckedInAt);

public record AbsentMember(string FullName, string? Phone, DateOnly? LastAttendance);

public static class AttendanceReportService
{
    // A member can have both a Culte and a Sainte Cène record for the same Sunday —
    // for general attendance purposes that's a single presence, not two, so keep only
    // the earliest (first arrival) record per member per Sunday.
    public static async Task<List<SundayPresence>> GetPresencesAsync(AppDbContext db, DateOnly start, DateOnly end)
    {
        var attendances = await db.Attendances
            .Include(a => a.Member)
            .Include(a => a.ServiceSession)
            .Where(a => a.ServiceSession!.Date >= start && a.ServiceSession!.Date <= end)
            .ToListAsync();

        return attendances
            .GroupBy(a => (a.MemberId, a.ServiceSessionId))
            .Select(g => g.OrderBy(a => a.CheckedInAt).First())
            .Select(a => new SundayPresence(a.MemberId, a.Member!.FullName, a.ServiceSession!.Date, a.CheckedInAt))
            .OrderByDescending(p => p.Date)
            .ThenBy(p => p.FullName)
            .ToList();
    }

    // Active members not in presentMemberIds, longest-absent first so the ones to
    // follow up with appear at the top.
    // IReadOnlyCollection (not a set) so Contains binds to Enumerable.Contains, which EF translates to SQL.
    public static async Task<List<AbsentMember>> GetAbsentMembersAsync(AppDbContext db, IReadOnlyCollection<int> presentMemberIds)
    {
        var absentMembersRaw = await db.Members
            .Where(m => m.IsActive && !presentMemberIds.Contains(m.Id))
            .Select(m => new
            {
                m.FirstName,
                m.LastName,
                m.Phone,
                LastAttendance = db.Attendances
                    .Where(a => a.MemberId == m.Id)
                    .OrderByDescending(a => a.ServiceSession!.Date)
                    .Select(a => (DateOnly?)a.ServiceSession!.Date)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return absentMembersRaw
            .Select(m => new AbsentMember($"{m.FirstName} {m.LastName}", m.Phone, m.LastAttendance))
            .OrderBy(m => m.LastAttendance ?? DateOnly.MinValue)
            .ToList();
    }
}
