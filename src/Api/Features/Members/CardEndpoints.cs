using System.Net;
using ChurchAttendance.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Features.Members;

public static class CardEndpoints
{
    public static void MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/card/{token}", async (string token, IAppDbContext db) =>
        {
            var member = await db.Members.FirstOrDefaultAsync(m => m.Token == token && m.IsActive);
            if (member is null)
            {
                // A member opens this link in a browser: answer with a page, not a JSON string.
                return Results.Content(CardNotFoundHtml, "text/html; charset=utf-8", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Text(BuildCardHtml(member.FullName, token), "text/html");
        });

        app.MapGet("/card/{token}/manifest.webmanifest", async (string token, IAppDbContext db) =>
        {
            var member = await db.Members.FirstOrDefaultAsync(m => m.Token == token && m.IsActive);
            if (member is null)
            {
                return Results.NotFound();
            }

            var manifest = new
            {
                name = $"Carte de {member.FullName}",
                short_name = member.FirstName,
                start_url = $"/card/{token}",
                display = "standalone",
                background_color = "#ffffff",
                theme_color = "#2c3e50",
                icons = new object[]
                {
                    new { src = "/card/icon-192.png", sizes = "192x192", type = "image/png" },
                    new { src = "/card/icon-512.png", sizes = "512x512", type = "image/png" }
                }
            };

            return Results.Json(manifest, contentType: "application/manifest+json");
        });
    }

    private const string CardNotFoundHtml = """
        <!DOCTYPE html>
        <html lang="fr">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Carte introuvable</title>
            <link rel="stylesheet" href="/card/style.css">
        </head>
        <body>
            <main class="card">
                <img class="banner" src="/branding/banniere-eglise.jpg" alt="Église De Dieu De Béthel" />
                <h1>Carte introuvable</h1>
                <p class="hint">Ce lien ne correspond à aucune carte active. Demandez une nouvelle carte au secrétariat.</p>
            </main>
        </body>
        </html>
        """;

    private static string BuildCardHtml(string fullName, string token)
    {
        var safeName = WebUtility.HtmlEncode(fullName);
        return $$"""
            <!DOCTYPE html>
            <html lang="fr">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
                <title>Carte de présence — {{safeName}}</title>
                <link rel="manifest" href="/card/{{token}}/manifest.webmanifest">
                <link rel="apple-touch-icon" sizes="180x180" href="/card/icon-180.png">
                <meta name="theme-color" content="#2c3e50">
                <link rel="stylesheet" href="/card/style.css">
            </head>
            <body>
                <main class="card">
                    <img class="banner" src="/branding/banniere-eglise.jpg" alt="Église De Dieu De Béthel" />
                    <h1>{{safeName}}</h1>
                    <img class="qr" src="/api/members/{{token}}/qr.png" alt="QR code de présence" />
                    <p class="hint">Présentez ce QR code à l'entrée du culte.</p>
                    <button id="install-btn" class="install-btn" hidden>Ajouter à l'écran d'accueil</button>
                    <p class="offline-note">Cette page fonctionne aussi sans connexion une fois ouverte une première fois.</p>
                </main>

                <div id="ios-instructions" class="ios-instructions-overlay" hidden>
                    <div class="ios-instructions-box">
                        <p>Pour ajouter cette carte à votre écran d'accueil :</p>
                        <ol>
                            <li>Appuyez sur l'icône Partager <strong>⬆️</strong> en bas de Safari</li>
                            <li>Choisissez <strong>« Sur l'écran d'accueil »</strong></li>
                            <li>Appuyez sur <strong>« Ajouter »</strong></li>
                        </ol>
                        <button id="ios-instructions-close" class="install-btn">Compris</button>
                    </div>
                </div>

                <script>
                    if ('serviceWorker' in navigator) {
                        navigator.serviceWorker.register('/card/sw.js', { scope: '/card/' });
                    }

                    const installBtn = document.getElementById('install-btn');
                    const isStandalone = window.navigator.standalone === true
                        || window.matchMedia('(display-mode: standalone)').matches;
                    const isIos = /iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream;

                    if (!isStandalone) {
                        if (isIos) {
                            installBtn.hidden = false;
                            installBtn.addEventListener('click', () => {
                                document.getElementById('ios-instructions').hidden = false;
                            });
                            document.getElementById('ios-instructions-close').addEventListener('click', () => {
                                document.getElementById('ios-instructions').hidden = true;
                            });
                        } else {
                            let deferredPrompt;
                            window.addEventListener('beforeinstallprompt', (e) => {
                                e.preventDefault();
                                deferredPrompt = e;
                                installBtn.hidden = false;
                                installBtn.addEventListener('click', async () => {
                                    installBtn.hidden = true;
                                    deferredPrompt.prompt();
                                    await deferredPrompt.userChoice;
                                    deferredPrompt = null;
                                });
                            });
                        }
                    }
                </script>
            </body>
            </html>
            """;
    }
}
