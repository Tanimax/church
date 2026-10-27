using ChurchAttendance.Data;
using ChurchAttendance.Identity.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ChurchAttendance.Infrastructure;

public static class DatabaseSetup
{
    public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = BuildPostgresConnectionString(
            configuration, Environment.GetEnvironmentVariable("DATABASE_URL"));

        // Read by each module's DbContext registration.
        services.AddSingleton(new DatabaseConnection(connectionString));

        // IDbContextFactory is used by Blazor Server components, which run on a long-lived circuit
        // and must create a short-lived context per operation instead of sharing one scoped instance.
        services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));

        // Minimal API endpoints inject AppDbContext directly (scoped, one instance per HTTP request),
        // derived from the same factory so there's a single DbContextOptions registration.
        services.AddScoped<AppDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        return services;
    }

    // Only used to seed the very first "admin" account — after that, credentials
    // live in the Users table and this variable is no longer consulted.
    public static string ResolveSeedAdminPassword(IConfiguration configuration, IHostEnvironment environment)
    {
        var adminPassword = configuration["AdminPassword"]
            ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

        if (!string.IsNullOrEmpty(adminPassword))
        {
            return adminPassword;
        }

        if (environment.IsDevelopment())
        {
            Console.WriteLine("ATTENTION: ADMIN_PASSWORD non défini — mot de passe de développement 'dev-only-password' utilisé.");
            return "dev-only-password";
        }

        throw new InvalidOperationException("La variable d'environnement ADMIN_PASSWORD doit être définie en production.");
    }

    public static async Task MigrateAndSeedAsync(this WebApplication app, string adminPassword)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        // Modules migrate after AppDbContext, whose migrations still create the tables they
        // map, and in ModulesSetup order (services resolve in registration order).
        foreach (var moduleDatabase in scope.ServiceProvider.GetServices<IModuleDatabase>())
        {
            await moduleDatabase.MigrateAsync();
        }

        await scope.ServiceProvider.GetRequiredService<IAdminSeeder>().SeedAdminIfNoUsersAsync(adminPassword);
    }

    // Fly Postgres (managed or unmanaged) exposes DATABASE_URL as a libpq-style URI
    // (postgres://user:pass@host:port/db), not the ADO.NET keyword=value format Npgsql
    // expects — parse it when there's no explicit ConnectionStrings:Default override.
    public static string BuildPostgresConnectionString(IConfiguration configuration, string? databaseUrl)
    {
        var explicitConnectionString = configuration.GetConnectionString("Default");
        if (!string.IsNullOrEmpty(explicitConnectionString))
        {
            return explicitConnectionString;
        }

        if (!string.IsNullOrEmpty(databaseUrl))
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':', 2);

            // Fly's internal Postgres (accessed over the private flycast network) doesn't
            // offer SSL and aborts the connection outright when a client asks to negotiate
            // it — respect the sslmode the platform put in DATABASE_URL instead of assuming.
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            var sslMode = query.TryGetValue("sslmode", out var sslModeValue)
                && Enum.TryParse<SslMode>(sslModeValue, ignoreCase: true, out var parsedSslMode)
                    ? parsedSslMode
                    : SslMode.Prefer;

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
                Database = uri.AbsolutePath.TrimStart('/'),
                SslMode = sslMode
            };
            return builder.ConnectionString;
        }

        return "Host=localhost;Port=5432;Database=churchattendance;Username=postgres;Password=postgres";
    }
}
