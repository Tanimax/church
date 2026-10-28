using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChurchAttendance.Features.CheckIn;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace ChurchAttendance.Tests.ErrorHandling;

// Every /api/* failure is a ProblemDetails JSON body with a French title and a traceId, so
// the scanner can tell the user what went wrong (docs/gestion-des-erreurs.md, § 3).
public class ApiErrorResponsesTests
{
    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(title, problem.GetProperty("title").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        return problem;
    }

    [Fact]
    public async Task InvalidJson_Is400ProblemDetails()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.PostAsync("/api/checkin", new StringContent("{pas du json", System.Text.Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Requête invalide.");
    }

    [Fact]
    public async Task NotLoggedIn_Is401ProblemDetails()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("card"));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Connexion requise.");
    }

    [Fact]
    public async Task UnknownApiRoute_Is404ProblemDetails_NotTheHtmlPage()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/n-existe-pas");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Introuvable.");
    }

    [Fact]
    public async Task UnexpectedFailure_Is500ProblemDetails_WithoutTechnicalDetails()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);
        // Make the database disappear under the running app: a real, non-transient failure.
        var databaseName = new NpgsqlConnectionStringBuilder(factory.ConnectionString).Database!;
        await using (var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(factory.ConnectionString) { Database = "postgres" }.ConnectionString))
        {
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }

        var response = await client.PostAsJsonAsync("/api/checkin", new CheckInRequest("card"));

        var problem = await AssertProblemAsync(response, HttpStatusCode.InternalServerError,
            "Une erreur inattendue est survenue. Réessayez dans un instant.");
        Assert.False(problem.TryGetProperty("detail", out _));
        Assert.DoesNotContain("Npgsql", problem.ToString());
    }
}
