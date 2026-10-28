using ChurchAttendance.Application.Members;
using ChurchAttendance.Application.Schedules;
using ChurchAttendance.Application.SundayClasses;
using ChurchAttendance.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<MemberService>();
        services.AddScoped<ScheduleService>();
        services.AddScoped<SundayClassService>();
        services.AddScoped<UserAccountService>();
        return services;
    }
}
