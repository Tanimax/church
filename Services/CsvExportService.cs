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

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
