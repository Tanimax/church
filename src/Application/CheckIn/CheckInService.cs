using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Domain;
using ChurchAttendance.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.CheckIn;

public record CheckInResponse(string Status, string? FullName, DateTime? CheckedInAt);

public static class CheckInService
{
    public static async Task<CheckInResponse> CheckInMemberAsync(IAppDbContext db, ChurchClock clock, Member member, AttendanceType type)
    {
        if (type == AttendanceType.SainteCene && !member.IsBaptized)
        {
            return new CheckInResponse("not_baptized", member.FullName, null);
        }

        var session = await GetOrCreateTodaysSessionAsync(db, clock);

        var response = await RecordAttendance(db, clock, member, session, type);

        // Being present at Sainte Cène implies being present at the regular service
        // that day — auto-record a Culte attendance too, unless one already exists.
        if (type == AttendanceType.SainteCene)
        {
            var hasCulte = await db.Attendances.AnyAsync(a =>
                a.MemberId == member.Id && a.ServiceSessionId == session.Id && a.Type == AttendanceType.Culte);
            if (!hasCulte)
            {
                await RecordAttendance(db, clock, member, session, AttendanceType.Culte, "Auto (Sainte Cène)");
            }
        }

        return response;
    }

    // The first scan of the day creates the service session. Two scanners often send that
    // first scan at the same moment (typically right after Fly wakes the app up), and the
    // unique index on Date then rejects the second insert: in that case the other request
    // won the race, so use the session it created.
    private static async Task<ServiceSession> GetOrCreateTodaysSessionAsync(IAppDbContext db, ChurchClock clock)
    {
        // The church's local date, not the server's (UTC on Fly).
        var today = clock.Today;
        var session = await db.ServiceSessions.FirstOrDefaultAsync(s => s.Date == today);
        if (session is not null)
        {
            return session;
        }

        session = new ServiceSession { Date = today, Label = "Culte du dimanche" };
        db.ServiceSessions.Add(session);
        try
        {
            await db.SaveChangesAsync();
            return session;
        }
        catch (DbUpdateException)
        {
            // Stop tracking the rejected insert, or every later SaveChanges would retry it.
            db.ServiceSessions.Remove(session);
            return await db.ServiceSessions.FirstAsync(s => s.Date == today);
        }
    }

    private static async Task<CheckInResponse> RecordAttendance(
        IAppDbContext db, ChurchClock clock, Member member, ServiceSession session, AttendanceType type, string? checkedInBy = null)
    {
        var existing = await db.Attendances
            .FirstOrDefaultAsync(a => a.MemberId == member.Id && a.ServiceSessionId == session.Id && a.Type == type);

        if (existing is not null)
        {
            return new CheckInResponse("duplicate", member.FullName, existing.CheckedInAt);
        }

        var attendance = new Attendance
        {
            MemberId = member.Id,
            ServiceSessionId = session.Id,
            CheckedInAt = clock.UtcNow,
            Type = type,
            CheckedInBy = checkedInBy
        };
        db.Attendances.Add(attendance);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Stop tracking the rejected insert, or the Culte auto-record that follows a
            // Sainte Cène scan would retry it in its own SaveChanges and fail too.
            db.Attendances.Remove(attendance);
            var raced = await db.Attendances
                .FirstOrDefaultAsync(a => a.MemberId == member.Id && a.ServiceSessionId == session.Id && a.Type == type);
            return new CheckInResponse("duplicate", member.FullName, raced?.CheckedInAt);
        }

        return new CheckInResponse("ok", member.FullName, attendance.CheckedInAt);
    }
}
