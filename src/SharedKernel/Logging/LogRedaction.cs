using System.Text.RegularExpressions;

namespace ChurchAttendance.SharedKernel.Logging;

// Personal data must not reach the logs in clear (docs/journalisation.md § PII): these helpers
// keep just enough to recognise a value when investigating.
public static partial class LogRedaction
{
    // "marie.dupont@gmail.com" -> "m***@gmail.com". The domain stays: it is what explains most
    // delivery failures (typo in the domain, provider rejecting the mail).
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "(vide)";
        }

        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return "***";
        }

        return $"{email[0]}***{email[at..]}";
    }

    // Masks every address found in a free text, e.g. an error body returned by the email API.
    public static string MaskEmailsIn(string? text) =>
        string.IsNullOrEmpty(text) ? "" : EmailAddress().Replace(text, match => MaskEmail(match.Value));

    [GeneratedRegex(@"[^\s@""'<>(),;:]+@[^\s@""'<>(),;:]+\.[^\s@""'<>(),;:]+")]
    private static partial Regex EmailAddress();
}
