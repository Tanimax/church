using System.Net;
using System.Security.Claims;
using ChurchAttendance.Data;
using ChurchAttendance.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/login", (HttpRequest request) =>
        {
            var hasError = request.Query.ContainsKey("error");
            var returnUrl = request.Query["returnUrl"].ToString();
            return Results.Text(BuildLoginHtml(hasError, returnUrl), "text/html");
        });

        app.MapPost("/admin/login", async (HttpContext context, AppDbContext db) =>
        {
            var form = await context.Request.ReadFormAsync();
            var username = UserService.NormalizeUsername(form["username"].ToString());
            var password = form["password"].ToString();
            var returnUrl = form["returnUrl"].ToString();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
            if (user is null || !UserService.VerifyPassword(user.PasswordHash, password))
            {
                var retry = "/admin/login?error=1";
                if (IsSafeLocalReturnUrl(returnUrl))
                {
                    retry += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
                }
                return Results.Redirect(retry);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, user.Role.ToString())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

            return Results.Redirect(IsSafeLocalReturnUrl(returnUrl) ? returnUrl : "/admin/members");
        });

        app.MapPost("/admin/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/admin/login");
        });

        // Lets the scanner PWA (a static page, not a Blazor component) find out the current
        // user's role client-side, so it can show only the check-in modes that role is allowed
        // to use.
        app.MapGet("/api/me", (ClaimsPrincipal user) =>
        {
            var role = user.FindFirst(ClaimTypes.Role)?.Value;
            return Results.Ok(new { role });
        }).RequireAuthorization();
    }

    // Only allow redirecting back to a same-site path after login (never a full URL),
    // otherwise a crafted returnUrl could send an admin's session to an attacker's site.
    private static bool IsSafeLocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//");

    private static string BuildLoginHtml(bool hasError, string? returnUrl = null)
    {
        var errorHtml = hasError
            ? "<p class=\"error\">Nom d'utilisateur ou mot de passe incorrect.</p>"
            : "";

        var returnUrlInput = IsSafeLocalReturnUrl(returnUrl)
            ? $"""<input type="hidden" name="returnUrl" value="{WebUtility.HtmlEncode(returnUrl)}" />"""
            : "";

        return $$"""
            <!DOCTYPE html>
            <html lang="fr">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Connexion admin</title>
                <link rel="preconnect" href="https://fonts.googleapis.com">
                <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
                <link href="https://fonts.googleapis.com/css2?family=Fraunces:opsz,wght@9..144,560&family=Public+Sans:wght@400;500;600;700&display=swap" rel="stylesheet">
                <style>
                    * { box-sizing: border-box; }
                    body { font-family: 'Public Sans', -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; background: #f5f4ef; display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0; padding: 1rem; }
                    .card { background: #fff; border-radius: 12px; box-shadow: 0 4px 20px rgba(27,36,54,0.08); padding: 1.75rem; max-width: 340px; width: 100%; border: 1px solid #e2ded2; }
                    h3 { font-family: 'Fraunces', Georgia, serif; font-weight: 560; margin: 0 0 1.1rem; color: #1b2436; }
                    label { display: block; margin-bottom: 0.4rem; font-weight: 500; color: #1b2436; }
                    input[type="text"], input[type="password"] { width: 100%; padding: 0.75rem; border: 1px solid #d3cebd; border-radius: 6px; font-size: 16px; margin-bottom: 1rem; font-family: inherit; }
                    button { width: 100%; padding: 0.75rem; background: #a8763a; color: #201304; border: none; border-radius: 6px; font-size: 1rem; font-weight: 600; cursor: pointer; }
                    button:hover { background: #8a5f28; }
                    .error { color: #dc2626; }
                </style>
            </head>
            <body>
                <div class="card">
                    <h3>Connexion admin</h3>
                    {{errorHtml}}
                    <form method="post" action="/admin/login">
                        {{returnUrlInput}}
                        <label for="username">Nom d'utilisateur</label>
                        <input type="text" id="username" name="username" required autofocus autocapitalize="none" autocomplete="username" />
                        <label for="password">Mot de passe</label>
                        <input type="password" id="password" name="password" required autocomplete="current-password" />
                        <button type="submit">Se connecter</button>
                    </form>
                </div>
            </body>
            </html>
            """;
    }
}
