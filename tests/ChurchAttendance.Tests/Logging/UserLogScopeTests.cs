using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ChurchAttendance.Features.CheckIn;
using ChurchAttendance.Startup;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Npgsql;

namespace ChurchAttendance.Tests.Logging;

// Who made the request is on every log line written while handling it (docs/journalisation.md).
public class UserLogScopeTests
{
    [Fact]
    public void UserScope_NamesTheStaffAccountAndItsRole()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "secretaire1"), new Claim(ClaimTypes.Role, "Secretaire")], "Cookies"));

        var scope = new LogScope(LoggingSetup.UserFields(user));

        Assert.Contains(new("UserName", "secretaire1"), scope);
        Assert.Contains(new("Role", "Secretaire"), scope);
        Assert.Equal("UserName=secretaire1 Role=Secretaire", scope.ToString());
    }

    [Fact]
    public void UserScope_MarksAnonymousVisitors()
    {
        var fields = LoggingSetup.UserFields(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Contains(new("UserName", "(anonyme)"), fields);
        Assert.Contains(new("Role", null), fields);
    }

    [Fact]
    public async Task ErrorLoggedDuringARequest_CarriesTheUser()
    {
        using var factory = new CustomWebApplicationFactory();
        using var app = factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddFakeLogging()));
        var client = await TestAuth.CreateAuthenticatedClientAsync(app);
        await DropDatabaseAsync(factory.ConnectionString);
        var logs = app.Services.GetFakeLogCollector();
        logs.Clear(); // startup lines (migrations) are not part of a request

        var response = await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("card"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var errors = logs.GetSnapshot().Where(r => r.Level == LogLevel.Error).ToList();
        // The failure itself is logged, with its exception (not only the database's own lines).
        Assert.Contains(errors, r => r.Category.EndsWith("ApiExceptionHandler") && r.Exception is not null);
        var withoutUser = errors
            .Where(record => !record.Scopes.Any(s =>
                s is IEnumerable<KeyValuePair<string, object?>> fields
                && fields.Contains(new("UserName", "admin"))
                && fields.Contains(new("Role", "Admin"))))
            .Select(record => $"{record.Category} [{string.Join(" | ", record.Scopes)}]")
            .ToList();
        Assert.True(withoutUser.Count == 0, "erreurs sans l'utilisateur : " + string.Join(", ", withoutUser));
    }

    private static async Task DropDatabaseAsync(string connectionString)
    {
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" }.ConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}
