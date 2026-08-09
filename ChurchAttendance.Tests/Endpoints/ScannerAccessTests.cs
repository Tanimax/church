using System.Net;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ChurchAttendance.Tests.Endpoints;

public class ScannerAccessTests
{
    [Fact]
    public async Task Scanner_WithoutAuthentication_RedirectsToLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/scanner");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/admin/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Scanner_WithoutAuthentication_RedirectIncludesReturnUrl()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/scanner");

        Assert.Contains("returnUrl=%2Fscanner", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ScannerStaticAsset_WithoutAuthentication_RedirectsToLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/scanner/index.html");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/admin/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Scanner_Authenticated_ReachesTheRealPage()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/scanner");

        // Authenticated requests fall through to the app's own /scanner -> /scanner/index.html
        // redirect (unrelated to the auth gate), not to the login page.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/scanner/index.html", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ScannerStaticAsset_Authenticated_ReturnsOk()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/scanner/index.html");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Login_WithReturnUrl_RedirectsBackToScannerAfterSuccess()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = TestAuth.BootstrapAdminUsername,
            ["password"] = TestAuth.BootstrapAdminPassword,
            ["returnUrl"] = "/scanner"
        });
        var response = await client.PostAsync("/admin/login", loginForm);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/scanner", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_WithExternalReturnUrl_IgnoresItAndRedirectsToDefault()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = TestAuth.BootstrapAdminUsername,
            ["password"] = TestAuth.BootstrapAdminPassword,
            ["returnUrl"] = "https://evil.example.com/phish"
        });
        var response = await client.PostAsync("/admin/login", loginForm);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/members", response.Headers.Location?.ToString());
    }
}
