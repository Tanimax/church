using System.Net;
using System.Net.Http.Json;

namespace ChurchAttendance.Services;

public class EmailService(HttpClient httpClient, IConfiguration config, ILogger<EmailService> logger)
{
    public async Task SendWelcomeEmailAsync(string toEmail, string fullName, string cardUrl)
    {
        var apiKey = config["ResendApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("ResendApiKey non configuré — courriel de bienvenue non envoyé à {Email}.", toEmail);
            return;
        }

        var fromAddress = config["ResendFromAddress"] ?? "onboarding@resend.dev";
        var safeName = WebUtility.HtmlEncode(fullName);

        var payload = new
        {
            from = fromAddress,
            to = new[] { toEmail },
            subject = "Bienvenue dans Church Attendance",
            html = $"""
                <div style="font-family:-apple-system,Helvetica,Arial,sans-serif;color:#333333;max-width:480px;margin:0 auto;">
                    <p>Bonjour {safeName},</p>
                    <p>Bienvenue dans Church Attendance !</p>
                    <p>Voici votre carte de membre — présentez son QR code à l'entrée du culte pour enregistrer votre présence :</p>
                    <p style="text-align:center;margin:32px 0;">
                        <a href="{cardUrl}" target="_blank"
                           style="display:inline-block;background-color:#2c3e50;color:#ffffff;text-decoration:none;
                                  padding:14px 28px;border-radius:8px;font-size:16px;font-weight:600;">
                            Voir ma carte de membre
                        </a>
                    </p>
                    <p style="font-size:13px;color:#888888;">
                        Si le bouton ne fonctionne pas, copiez ce lien dans votre navigateur :<br>
                        <a href="{cardUrl}" style="color:#2c3e50;">{cardUrl}</a>
                    </p>
                </div>
                """
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(payload);

            var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                logger.LogWarning("Échec de l'envoi du courriel à {Email} : {Status} {Body}", toEmail, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: a failed email must never block member creation.
            logger.LogWarning(ex, "Erreur lors de l'envoi du courriel de bienvenue à {Email}.", toEmail);
        }
    }
}
