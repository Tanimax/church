using System.Net;
using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services;

namespace ChurchAttendance.Endpoints;

public static class VisitorEndpoints
{
    public static void MapVisitorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/visitor", (HttpRequest request) =>
        {
            var status = request.Query["status"].ToString();
            return Results.Text(BuildVisitorFormHtml(status), "text/html");
        });

        app.MapPost("/api/visitors", async (HttpContext context, AppDbContext db) =>
        {
            var form = await context.Request.ReadFormAsync();
            var fullName = form["fullName"].ToString().Trim();
            var phone = form["phone"].ToString().Trim();
            var email = form["email"].ToString().Trim();

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone))
            {
                return Results.Redirect("/visitor?status=error");
            }

            var visitor = new Visitor
            {
                FullName = fullName,
                Phone = phone,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                VisitDate = DateOnly.FromDateTime(DateTime.Now),
                CreatedAt = DateTime.UtcNow
            };
            db.Visitors.Add(visitor);
            await db.SaveChangesAsync();

            return Results.Redirect("/visitor?status=success");
        });

        app.MapGet("/visitor/qr.png", (HttpRequest request) =>
        {
            var baseUrl = $"{request.Scheme}://{request.Host}";
            var png = QrCodeService.GeneratePng($"{baseUrl}/visitor");
            return Results.File(png, "image/png");
        });
    }

    private static string BuildVisitorFormHtml(string status)
    {
        if (status == "success")
        {
            return """
                <!DOCTYPE html>
                <html lang="fr">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>Merci de votre visite</title>
                    <link rel="stylesheet" href="/visitor/style.css">
                </head>
                <body>
                    <main class="card">
                        <h1>Merci de votre visite ! 🙏</h1>
                        <p>Vos coordonnées ont bien été enregistrées. Nous espérons vous revoir bientôt.</p>
                    </main>
                </body>
                </html>
                """;
        }

        var errorHtml = status == "error"
            ? "<p class=\"error\">Le nom complet et le téléphone sont requis.</p>"
            : "";

        return $$"""
            <!DOCTYPE html>
            <html lang="fr">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Fiche visiteur</title>
                <link rel="stylesheet" href="/visitor/style.css">
            </head>
            <body>
                <main class="card">
                    <h1>Bienvenue !</h1>
                    <p class="hint">Nous sommes heureux de vous accueillir. Remplissez ce court formulaire pour rester en contact.</p>
                    {{errorHtml}}
                    <form method="post" action="/api/visitors">
                        <label for="fullName">Nom complet</label>
                        <input type="text" id="fullName" name="fullName" required autofocus />

                        <label for="phone">Téléphone</label>
                        <input type="tel" id="phone" name="phone" required />

                        <label for="email">Courriel (optionnel)</label>
                        <input type="email" id="email" name="email" />

                        <button type="submit">Envoyer</button>
                    </form>
                </main>
            </body>
            </html>
            """;
    }
}
