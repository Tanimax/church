using System.Data.Common;
using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ChurchAttendance.Infrastructure.Persistence;

public static class DatabaseSetup
{
    public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = WithDefaultPoolLimit(BuildPostgresConnectionString(
            configuration, Environment.GetEnvironmentVariable("DATABASE_URL")));

        // IDbContextFactory is used by Blazor Server components, which run on a long-lived circuit
        // and must create a short-lived context per operation instead of sharing one scoped instance.
        services.AddDbContextFactory<AppDbContext>(options => ConfigureNpgsql(options, connectionString));

        // Minimal API endpoints inject AppDbContext directly (scoped, one instance per HTTP request),
        // derived from the same factory so there's a single DbContextOptions registration.
        services.AddScoped<AppDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        // What Application and the pages actually depend on.
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IAppDbContextFactory, AppDbContextFactory>();

        return services;
    }

    // Transient failures (the database restarting, a dropped connection, Fly waking a stopped
    // machine) are retried a few times before the error reaches the user. No code opens an
    // explicit transaction, which this retrying strategy would require to be wrapped in
    // Database.CreateExecutionStrategy().ExecuteAsync.
    public static void ConfigureNpgsql(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorCodesToAdd: null));

    // Development fallback for the first admin's password.
    public const string DevelopmentAdminPassword = "dev-only-password";

    // Only used to seed the very first "admin" account — after that, credentials
    // live in the Users table and this variable is no longer consulted.
    // IsDevelopmentFallback tells the caller to warn (the logger only exists after Build).
    public static (string Password, bool IsDevelopmentFallback) ResolveSeedAdminPassword(
        IConfiguration configuration, IHostEnvironment environment)
    {
        var adminPassword = configuration["AdminPassword"]
            ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

        if (!string.IsNullOrEmpty(adminPassword))
        {
            return (adminPassword, false);
        }

        if (environment.IsDevelopment())
        {
            return (DevelopmentAdminPassword, true);
        }

        throw new InvalidOperationException("La variable d'environnement ADMIN_PASSWORD doit être définie en production.");
    }

    public static async Task MigrateAndSeedAsync(this IHost app, string adminPassword)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        await scope.ServiceProvider.GetRequiredService<UserAccountService>().SeedAdminIfNoUsersAsync(adminPassword);
    }

    // Npgsql's default pool allows 100 connections, which is also Postgres's default
    // max_connections for the whole server (shared here with the staging database): a burst
    // of requests could take every connection the server has. In a load test, 200
    // simultaneous check-ins on one CPU were answered faster with 20 connections (~0.7 s)
    // than with 100 (~1.7 s), so cap the pool at 20 unless the connection string sets its own.
    public const int DefaultMaxPoolSize = 20;

    public static string WithDefaultPoolLimit(string connectionString)
    {
        // NpgsqlConnectionStringBuilder reports every setting as present (with its default),
        // so look at the keys actually written in the string. Npgsql accepts both spellings.
        var writtenKeys = new DbConnectionStringBuilder { ConnectionString = connectionString }.Keys
            .Cast<string>()
            .Select(k => k.Replace(" ", "").ToLowerInvariant());
        if (writtenKeys.Any(k => k is "maximumpoolsize" or "maxpoolsize"))
        {
            return connectionString;
        }

        return new NpgsqlConnectionStringBuilder(connectionString) { MaxPoolSize = DefaultMaxPoolSize }.ConnectionString;
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
