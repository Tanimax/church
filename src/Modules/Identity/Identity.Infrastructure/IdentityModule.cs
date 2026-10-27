using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Identity.Infrastructure;

// User accounts, login/logout and roles.
public sealed class IdentityModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Same registrations as AppDbContext: a factory for Blazor components, and a scoped
        // context derived from it for anything resolved per request.
        services.AddDbContextFactory<IdentityDbContext>((sp, options) => options.UseNpgsql(
            sp.GetRequiredService<DatabaseConnection>().ConnectionString,
            npgsql => npgsql.MigrationsHistoryTable(IdentityDbContext.MigrationsHistoryTable)));
        services.AddScoped<IdentityDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<IdentityDbContext>>().CreateDbContext());

        services.AddScoped<UserAccountService>();
        services.AddScoped<IAdminSeeder, AdminSeeder>();
        services.AddScoped<IModuleDatabase, IdentityDatabase>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => app.MapAuthEndpoints();
}

internal sealed class IdentityDatabase(IdentityDbContext db) : IModuleDatabase
{
    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        db.Database.MigrateAsync(cancellationToken);
}

internal sealed class AdminSeeder(IdentityDbContext db) : IAdminSeeder
{
    // One-time bootstrap: the app used to gate the whole admin area behind a single shared
    // ADMIN_PASSWORD. The first time this runs against a database with no user accounts yet,
    // it creates an "admin" account with that same password so existing deployments keep
    // working without any manual step.
    public async Task SeedAdminIfNoUsersAsync(string password, CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Users.Add(User.Create("admin", PasswordHashing.HashPassword(password), UserRole.Admin, DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
    }
}
