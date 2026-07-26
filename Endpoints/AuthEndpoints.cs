using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ChurchAttendance.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/login", (HttpRequest request) =>
        {
            var hasError = request.Query.ContainsKey("error");
            return Results.Text(BuildLoginHtml(hasError), "text/html");
        });

        app.MapPost("/admin/login", async (HttpContext context, IConfiguration config) =>
        {
            var form = await context.Request.ReadFormAsync();
            var password = form["password"].ToString();
            var expectedPassword = config["AdminPassword"];

            if (string.IsNullOrEmpty(expectedPassword) || password != expectedPassword)
            {
                return Results.Redirect("/admin/login?error=1");
            }

            var claims = new List<Claim> { new(ClaimTypes.Name, "admin") };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

            return Results.Redirect("/admin/members");
        });

        app.MapPost("/admin/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/admin/login");
        });
    }

    private static string BuildLoginHtml(bool hasError)
    {
        var errorHtml = hasError
            ? "<p class=\"error\">Mot de passe incorrect.</p>"
            : "";

        return $$"""
            <!DOCTYPE html>
            <html lang="fr">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Connexion admin</title>
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; background: #f4f6f8; display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0; }
                    .card { background: #fff; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.08); padding: 2rem; max-width: 340px; width: 100%; }
                    label { display: block; margin-bottom: 0.4rem; font-weight: 500; }
                    input[type="password"] { width: 100%; padding: 0.5rem; border: 1px solid #ccc; border-radius: 6px; box-sizing: border-box; margin-bottom: 1rem; }
                    button { width: 100%; padding: 0.6rem; background: #2c3e50; color: #fff; border: none; border-radius: 6px; font-size: 1rem; cursor: pointer; }
                    .error { color: #dc2626; }
                </style>
            </head>
            <body>
                <div class="card">
                    <h3>Connexion admin</h3>
                    {{errorHtml}}
                    <form method="post" action="/admin/login">
                        <label for="password">Mot de passe</label>
                        <input type="password" id="password" name="password" required autofocus />
                        <button type="submit">Se connecter</button>
                    </form>
                </div>
            </body>
            </html>
            """;
    }
}
