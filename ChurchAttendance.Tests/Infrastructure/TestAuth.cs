using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ChurchAttendance.Tests.Infrastructure;

public static class TestAuth
{
    // Program.cs falls back to this password in the Development environment (which
    // CustomWebApplicationFactory always uses) when ADMIN_PASSWORD isn't set, and seeds
    // a bootstrap "admin" account with it the first time a fresh database has no users.
    public const string BootstrapAdminUsername = "admin";
    public const string BootstrapAdminPassword = "dev-only-password";

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        CustomWebApplicationFactory factory,
        string username = BootstrapAdminUsername,
        string password = BootstrapAdminPassword)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password
        });
        var loginResponse = await client.PostAsync("/admin/login", loginForm);
        if (loginResponse.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Test login failed with status {loginResponse.StatusCode}");
        }
        return client;
    }
}
