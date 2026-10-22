namespace ChurchAttendance.Endpoints;

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
