using ChurchAttendance.Features.CheckIn;
using ChurchAttendance.Features.Members;
using ChurchAttendance.Features.Reports;
using ChurchAttendance.Features.Users;
using ChurchAttendance.Features.Visitors;
namespace ChurchAttendance.Infrastructure;

public static class EndpointsSetup
{
    public static WebApplication MapAppEndpoints(this WebApplication app)
    {
        app.MapMembersEndpoints();
        app.MapCardEndpoints();
        app.MapCheckInEndpoints();
        app.MapReportsEndpoints();
        app.MapAuthEndpoints();
        app.MapVisitorEndpoints();

        app.MapGet("/scanner", () => Results.Redirect("/scanner/index.html"));
        return app;
    }
}
