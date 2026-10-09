using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Endpoints;

public record CheckInRequest(string Token, AttendanceType Type = AttendanceType.Culte);

public static class CheckInEndpoints
{
    public static void MapCheckInEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/checkin", async (CheckInRequest request, AppDbContext db) =>
        {
            var member = await db.Members.FirstOrDefaultAsync(m => m.Token == request.Token && m.IsActive);
            if (member is null)
            {
                return Results.Ok(new CheckInResponse("not_found", null, null));
            }

            var response = await CheckInService.CheckInMemberAsync(db, member, request.Type);
            return Results.Ok(response);
        });
    }
}
