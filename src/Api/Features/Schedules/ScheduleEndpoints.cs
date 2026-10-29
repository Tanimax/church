using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Schedules;
using ChurchAttendance.Startup;

namespace ChurchAttendance.Features.Schedules;

public static class ScheduleEndpoints
{
    public static void MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        // The quarter's sheet as saved (not what is being typed on the page), ready to print or send.
        app.MapGet("/admin/programmation/{year:int:range(2000,2100)}/{quarter:int:range(1,4)}/pdf",
            async (int year, int quarter, ScheduleService schedules, ISchedulePdfRenderer renderer) =>
            {
                var sheet = ScheduleSheet.From(await schedules.GetAsync(year, quarter));
                var fileName = $"Service_dominical_{sheet.Period.Replace("Du ", "du ").Replace(' ', '_')}.pdf";
                return Results.File(renderer.Render(sheet), "application/pdf", fileName);
            }).RequireAuthorization(AuthorizationPolicies.ManageCommunication);
    }
}
