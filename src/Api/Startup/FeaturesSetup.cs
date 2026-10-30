using ChurchAttendance.Features.Announcements;
using ChurchAttendance.Features.CheckIn;
using ChurchAttendance.Features.Members;
using ChurchAttendance.Features.Reports;
using ChurchAttendance.Features.Schedules;
using ChurchAttendance.Features.Users;
using ChurchAttendance.Features.Visitors;

namespace ChurchAttendance.Startup;

// The single place that maps every feature's HTTP endpoints (pages are routed by Blazor).
public static class FeaturesSetup
{
    public static WebApplication MapAppEndpoints(this WebApplication app)
    {
        app.MapMembersEndpoints();
        app.MapCardEndpoints();
        app.MapCheckInEndpoints();
        app.MapReportsEndpoints();
        app.MapScheduleEndpoints();
        app.MapAnnouncementEndpoints();
        app.MapVisitorEndpoints();
        app.MapAuthEndpoints();

        app.MapGet("/scanner", () => Results.Redirect("/scanner/index.html"));
        return app;
    }
}
