using ChurchAttendance.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ChurchAttendance.Tests.Infrastructure;

public class DatabaseSetupTests
{
    private static IConfiguration Config(string? defaultConnectionString = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = defaultConnectionString
            })
            .Build();

    [Fact]
    public void BuildPostgresConnectionString_ExplicitConnectionStringWins()
    {
        var result = DatabaseSetup.BuildPostgresConnectionString(
            Config("Host=explicit;Database=x"), "postgres://u:p@fly-host:5433/db");

        Assert.Equal("Host=explicit;Database=x", result);
    }

    [Fact]
    public void BuildPostgresConnectionString_ParsesFlyDatabaseUrl()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseSetup.BuildPostgresConnectionString(
            Config(), "postgres://app%40user:p%3Ass@db.flycast:5433/church?sslmode=disable"));

        Assert.Equal("db.flycast", result.Host);
        Assert.Equal(5433, result.Port);
        Assert.Equal("app@user", result.Username);
        Assert.Equal("p:ss", result.Password);
        Assert.Equal("church", result.Database);
        Assert.Equal(SslMode.Disable, result.SslMode);
    }

    [Fact]
    public void BuildPostgresConnectionString_DefaultsPortAndSslMode()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseSetup.BuildPostgresConnectionString(
            Config(), "postgres://u:p@host/db"));

        Assert.Equal(5432, result.Port);
        Assert.Equal(SslMode.Prefer, result.SslMode);
    }

    [Fact]
    public void BuildPostgresConnectionString_FallsBackToLocalDatabase()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseSetup.BuildPostgresConnectionString(Config(), null));

        Assert.Equal("localhost", result.Host);
        Assert.Equal("churchattendance", result.Database);
    }
}
