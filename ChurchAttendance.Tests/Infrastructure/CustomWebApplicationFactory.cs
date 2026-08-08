using ChurchAttendance.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ChurchAttendance.Tests.Infrastructure;

// Hosts the real app (Program.cs) against a real PostgreSQL database so endpoint
// tests exercise real provider behavior (unique indexes, migrations, DateOnly
// mapping) instead of a stand-in provider. Containers are slow to start, so a
// single Postgres container is shared for the whole test run (via Ryuk, it's
// reaped automatically once the test process exits); each factory instance gets
// its own freshly created, migrated database on that container for isolation.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly PostgreSqlContainer Container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private static readonly SemaphoreSlim StartLock = new(1, 1);
    private static bool _started;

    private readonly string _databaseName = $"test_{Guid.NewGuid():N}";
    private readonly string _connectionString;

    private static void EnsureContainerStarted()
    {
        if (_started)
        {
            return;
        }

        StartLock.Wait();
        try
        {
            if (!_started)
            {
                Container.StartAsync().GetAwaiter().GetResult();
                _started = true;
            }
        }
        finally
        {
            StartLock.Release();
        }
    }

    public CustomWebApplicationFactory()
    {
        EnsureContainerStarted();

        var adminConnectionString = Container.GetConnectionString();
        using (var adminConnection = new NpgsqlConnection(adminConnectionString))
        {
            adminConnection.Open();
            using var createCmd = adminConnection.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
            createCmd.ExecuteNonQuery();
        }

        _connectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = _databaseName
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextFactory<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(_connectionString));
            services.AddScoped<AppDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
        });
    }

    public AppDbContext CreateDbContext() =>
        Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        try
        {
            using var adminConnection = new NpgsqlConnection(Container.GetConnectionString());
            adminConnection.Open();
            using var dropCmd = adminConnection.CreateCommand();
            dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
            dropCmd.ExecuteNonQuery();
        }
        catch
        {
            // Best-effort cleanup — the whole container is torn down after the test run regardless.
        }
    }
}
