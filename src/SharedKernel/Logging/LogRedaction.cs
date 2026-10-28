namespace ChurchAttendance.SharedKernel.Logging;

// Personal data must not reach the logs in clear (docs/journalisation.md § PII): these helpers
// keep just enough to recognise a value when investigating.
public static class LogRedaction
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
}
