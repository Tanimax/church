using ChurchAttendance.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChurchAttendance.Tests.Infrastructure;

// Hosts the real app (Program.cs) against a private, in-memory SQLite database
// so endpoint tests exercise real EF Core behavior (unique indexes, migrations)
// without touching the developer's actual churchattendance.db file.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public CustomWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextFactory<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<AppDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
        });
    }

    public AppDbContext CreateDbContext() =>
        Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
