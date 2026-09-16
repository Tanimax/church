using System.Net;
using ChurchAttendance.Models;
using ChurchAttendance.Services;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ChurchAttendance.Tests.Endpoints;

public class AuthEndpointsTests
{
    [Fact]
    public async Task Login_BootstrapAdmin_SucceedsAndRedirectsToMembers()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = TestAuth.BootstrapAdminUsername,
            ["password"] = TestAuth.BootstrapAdminPassword
        });
        var response = await client.PostAsync("/admin/login", loginForm);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/members", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_UsernameIsCaseInsensitive()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "ADMIN",
            ["password"] = TestAuth.BootstrapAdminPassword
        });
        var response = await client.PostAsync("/admin/login", loginForm);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/members", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_WrongPassword_RedirectsWithError()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = TestAuth.BootstrapAdminUsername,
            ["password"] = "wrong-password"
        });
        var response = await client.PostAsync("/admin/login", loginForm);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/admin/login?error=1", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_UnknownUsername_RedirectsWithError()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "does-not-exist",
            ["password"] = "whatever"
        });
        var response = await client.PostAsync("/admin/login", loginForm);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/admin/login?error=1", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_InactiveUser_IsRejected()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Users.Add(new User
            {
                Username = "disabled",
                PasswordHash = UserService.HashPassword("some-password"),
                Role = UserRole.Secretaire,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "disabled",
            ["password"] = "some-password"
        });
        var response = await client.PostAsync("/admin/login", loginForm);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/admin/login?error=1", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_SecretaireRole_CanStillLogInAndReachAuthorizedPages()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Users.Add(new User
            {
                Username = "secretaire1",
                PasswordHash = UserService.HashPassword("secretaire-password"),
                Role = UserRole.Secretaire,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "secretaire1", "secretaire-password");

        // [Authorize] on these pages accepts any authenticated role — Secrétaire "peut tout voir".
        var membersResponse = await client.GetAsync("/admin/members");
        var usersResponse = await client.GetAsync("/admin/users");

        Assert.Equal(HttpStatusCode.OK, membersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
    }

    [Fact]
    public async Task UsersPage_WithoutAuthentication_RedirectsToLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/admin/users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/admin/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task LoginPage_Default_DoesNotShowIdleNotice()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var html = await client.GetStringAsync("/admin/login");

        Assert.DoesNotContain("déconnecté après une période d'inactivité", html);
    }

    [Fact]
    public async Task LoginPage_WithIdleQueryParam_ShowsIdleNotice()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var html = await client.GetStringAsync("/admin/login?idle=1");

        Assert.Contains("déconnecté après une période d'inactivité", html);
    }

    [Fact]
    public async Task LoginPage_WithErrorAndIdleQueryParams_ShowsErrorNotIdleNotice()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var html = await client.GetStringAsync("/admin/login?error=1&idle=1");

        Assert.Contains("incorrect", html);
        Assert.DoesNotContain("déconnecté après une période d'inactivité", html);
    }

    [Fact]
    public async Task Logout_EndsSession_SubsequentRequestToAuthorizedPageRedirectsToLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var beforeLogout = await client.GetAsync("/admin/members");
        Assert.Equal(HttpStatusCode.OK, beforeLogout.StatusCode);

        var logoutResponse = await client.PostAsync("/admin/logout", content: null);
        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.StartsWith("/admin/login", logoutResponse.Headers.Location?.ToString());

        var afterLogout = await client.GetAsync("/admin/members");
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Contains("/admin/login", afterLogout.Headers.Location?.ToString());
    }

    [Fact]
    public async Task BootstrapAdmin_IsSeededExactlyOnceOnFreshDatabase()
    {
        using var factory = new CustomWebApplicationFactory();
        await using var db = factory.CreateDbContext();

        var admins = db.Users.Where(u => u.Username == TestAuth.BootstrapAdminUsername).ToList();

        Assert.Single(admins);
        Assert.Equal(UserRole.Admin, admins[0].Role);
        Assert.True(admins[0].IsActive);
    }
}
