using System.Text;

namespace ChurchAttendance.Services;

public static class CsvExportService
{
    public static string BuildAttendanceCsv(IEnumerable<(DateOnly Date, string FullName, DateTime CheckedInAt)> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Nom,HeureArrivee");
        foreach (var row in rows)
        {
            sb.AppendLine($"{row.Date:yyyy-MM-dd},{EscapeCsv(row.FullName)},{row.CheckedInAt:yyyy-MM-dd HH:mm}");
        }
        return sb.ToString();
    }

    public static string BuildMembersCsv(IEnumerable<(string LastName, string FirstName, string? Phone, string? BirthdayLabel, bool IsBaptized, bool IsActive)> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Nom,Prenom,Telephone,Naissance,Baptise,Statut");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',',
                EscapeCsv(row.LastName),
                EscapeCsv(row.FirstName),
                EscapeCsv(row.Phone ?? ""),
                EscapeCsv(row.BirthdayLabel ?? ""),
                row.IsBaptized ? "Oui" : "Non",
                row.IsActive ? "Actif" : "Inactif"));
        }
        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }

    // Excel only renders accented characters (é, è, ç...) correctly from a CSV when a UTF-8
    // BOM is present — without it, opening the file directly in Excel mangles names like
    // "Cène" or "Désactivé". Encoding.UTF8.GetBytes alone doesn't include the BOM.
    public static byte[] ToCsvFileBytes(string csv) =>
        [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv)];
}
