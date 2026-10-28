using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.CheckIn;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Features.CheckIn;

public record CheckInRequest(string Token, AttendanceType Type = AttendanceType.Culte);

public static class CheckInEndpoints
{
    public static void MapCheckInEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/checkin", async (CheckInRequest request, IAppDbContext db, ChurchClock clock) =>
        {
            var member = await db.Members.FirstOrDefaultAsync(m => m.Token == request.Token && m.IsActive);
            if (member is null)
            {
                return Results.Ok(new CheckInResponse("not_found", null, null));
            }

            var response = await CheckInService.CheckInMemberAsync(db, clock, member, request.Type);
            return Results.Ok(response);
        }).RequireAuthorization(); // Only logged-in scanners: a card token is in its public /card URL.
    }
}
