using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace ChurchAttendance.Startup;

public static class SecuritySetup
{
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services)
    {
        services.AddCascadingAuthenticationState();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/admin/login";
                options.AccessDeniedPath = "/admin/login";
                options.Cookie.Name = "ChurchAttendance.Admin";

                // JSON API calls (the scanner's fetch) get a status code they can act on
                // instead of a redirect to the HTML login page.
                options.Events.OnRedirectToLogin = context => RedirectOrStatus(context, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context => RedirectOrStatus(context, StatusCodes.Status403Forbidden);
            });
        services.AddAuthorization();
        return services;
    }

    private static Task RedirectOrStatus(RedirectContext<CookieAuthenticationOptions> context, int apiStatusCode)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            // The API's status code pages then write it as ProblemDetails (ErrorHandlingSetup).
            context.Response.StatusCode = apiStatusCode;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }
        return Task.CompletedTask;
    }

    // By default, ASP.NET Core generates a fresh Data Protection key on every process start
    // and keeps it only in the container's ephemeral filesystem. Since this app scales to zero
    // and restarts on each new request, that would invalidate every antiforgery/circuit token
    // (and the admin auth cookie) on every cold start. Persisting keys to the mounted volume
    // keeps them stable across restarts.
    public static IServiceCollection AddPersistentDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var dataProtectionKeysPath = configuration["DataProtectionKeysPath"];
        if (!string.IsNullOrEmpty(dataProtectionKeysPath))
        {
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
        }
        return services;
    }

    // Fly.io terminates TLS at its edge proxy and forwards plain HTTP to the container,
    // so the app must trust X-Forwarded-Proto to know the original request was HTTPS
    // (otherwise generated links like the member card URL come out as http://).
    public static WebApplication UseFlyForwardedHeaders(this WebApplication app)
    {
        var forwardedHeadersOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };
        forwardedHeadersOptions.KnownIPNetworks.Clear();
        forwardedHeadersOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedHeadersOptions);
        return app;
    }

    // The scanner is served as static files, so [Authorize] never applies to it — gate it
    // here instead, before static file serving, since anyone with the URL could otherwise
    // use it to record attendance without being an admin.
    public static WebApplication UseScannerAuthGate(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/scanner") && context.User.Identity?.IsAuthenticated != true)
            {
                var returnUrl = context.Request.Path + context.Request.QueryString;
                context.Response.Redirect($"/admin/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
                return;
            }
            await next();
        });
        return app;
    }
}
