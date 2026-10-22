using ChurchAttendance.Features.CheckIn;
using ChurchAttendance.Features.Members;
using ChurchAttendance.Features.Reports;
using ChurchAttendance.Features.SundayClasses;
using ChurchAttendance.Features.Users;
using ChurchAttendance.Features.Visitors;

namespace ChurchAttendance.Infrastructure;

// The single place that wires each feature's services and routes into the app.
public static class FeaturesSetup
{
    public static IServiceCollection AddAppFeatures(this IServiceCollection services)
    {
        services.AddHttpClient<EmailService>();
        services.AddScoped<SundayClassService>();
        services.AddScoped<UserAccountService>();
        services.AddScoped<MemberService>();
        return services;
    }

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
