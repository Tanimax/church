using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Infrastructure.Cards;
using ChurchAttendance.Infrastructure.Documents;
using ChurchAttendance.Infrastructure.Email;
using ChurchAttendance.Infrastructure.Persistence;
using ChurchAttendance.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAppDatabase(configuration);
        // HttpClient's default timeout is 100 s: a slow email API would freeze the member form.
        services.AddHttpClient<IEmailSender, EmailService>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IMemberCardRenderer, MemberCardRenderer>();
        services.AddSingleton<ISchedulePdfRenderer, SchedulePdfRenderer>();
        return services;
    }
}
