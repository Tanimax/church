using ChurchAttendance.Data;
using ChurchAttendance.Models;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Endpoints;

public record CheckInRequest(string Token, AttendanceType Type = AttendanceType.Culte);

public record CheckInResponse(string Status, string? FullName, string? PhotoUrl, DateTime? CheckedInAt);

public static class CheckInEndpoints
{
    public static void MapCheckInEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/checkin", async (CheckInRequest request, AppDbContext db) =>
        {
            var member = await db.Members.FirstOrDefaultAsync(m => m.Token == request.Token && m.IsActive);
            if (member is null)
            {
                return Results.Ok(new CheckInResponse("not_found", null, null, null));
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            var session = await db.ServiceSessions.FirstOrDefaultAsync(s => s.Date == today);
            if (session is null)
            {
                session = new ServiceSession { Date = today, Label = "Culte du dimanche" };
                db.ServiceSessions.Add(session);
                await db.SaveChangesAsync();
            }

            var response = await RecordAttendance(db, member, session, request.Type);

            // Being present at Sainte Cène implies being present at the regular service
            // that day — auto-record a Culte attendance too, unless one already exists.
            if (request.Type == AttendanceType.SainteCene)
            {
                var hasCulte = await db.Attendances.AnyAsync(a =>
                    a.MemberId == member.Id && a.ServiceSessionId == session.Id && a.Type == AttendanceType.Culte);
                if (!hasCulte)
                {
                    await RecordAttendance(db, member, session, AttendanceType.Culte, "Auto (Sainte Cène)");
                }
            }

            return Results.Ok(response);
        });
    }

    private static async Task<CheckInResponse> RecordAttendance(
        AppDbContext db, Member member, ServiceSession session, AttendanceType type, string? checkedInBy = null)
    {
        var existing = await db.Attendances
            .FirstOrDefaultAsync(a => a.MemberId == member.Id && a.ServiceSessionId == session.Id && a.Type == type);

        if (existing is not null)
        {
            return new CheckInResponse("duplicate", member.FullName, member.PhotoUrl, existing.CheckedInAt);
        }

        var attendance = new Attendance
        {
            MemberId = member.Id,
            ServiceSessionId = session.Id,
            CheckedInAt = DateTime.UtcNow,
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
            var raced = await db.Attendances
                .FirstOrDefaultAsync(a => a.MemberId == member.Id && a.ServiceSessionId == session.Id && a.Type == type);
            return new CheckInResponse("duplicate", member.FullName, member.PhotoUrl, raced?.CheckedInAt);
        }

        return new CheckInResponse("ok", member.FullName, member.PhotoUrl, attendance.CheckedInAt);
    }
}
