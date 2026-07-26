using ChurchAttendance.Services;

namespace ChurchAttendance.Tests.Services;

public class CsvExportServiceTests
{
    // CsvExportService writes lines via StringBuilder.AppendLine, which uses
    // Environment.NewLine ("\n" on Linux/macOS, "\r\n" on Windows) — match that here.
    private static readonly string NL = Environment.NewLine;

    [Fact]
    public void BuildAttendanceCsv_NoRows_ReturnsHeaderOnly()
    {
        var csv = CsvExportService.BuildAttendanceCsv([]);

        Assert.Equal($"Date,Nom,HeureArrivee{NL}", csv);
    }

    [Fact]
    public void BuildAttendanceCsv_FormatsDateAndTime()
    {
        var rows = new[]
        {
            (Date: new DateOnly(2026, 7, 26), FullName: "Jean Dupont", CheckedInAt: new DateTime(2026, 7, 26, 9, 5, 30))
        };

        var csv = CsvExportService.BuildAttendanceCsv(rows);

        Assert.Equal($"Date,Nom,HeureArrivee{NL}2026-07-26,Jean Dupont,2026-07-26 09:05{NL}", csv);
    }

    [Fact]
    public void BuildAttendanceCsv_MultipleRows_PreservesOrder()
    {
        var rows = new[]
        {
            (Date: new DateOnly(2026, 7, 26), FullName: "Alice", CheckedInAt: new DateTime(2026, 7, 26, 9, 0, 0)),
            (Date: new DateOnly(2026, 7, 19), FullName: "Bob", CheckedInAt: new DateTime(2026, 7, 19, 9, 0, 0))
        };

        var csv = CsvExportService.BuildAttendanceCsv(rows);
        var lines = csv.Split(NL, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.Contains("Alice", lines[1]);
        Assert.Contains("Bob", lines[2]);
    }

    [Theory]
    [InlineData("Dupont, Jean", "\"Dupont, Jean\"")]
    [InlineData("Jean \"Le Grand\" Dupont", "\"Jean \"\"Le Grand\"\" Dupont\"")]
    [InlineData("Jean\nDupont", "\"Jean\nDupont\"")]
    [InlineData("Jean Dupont", "Jean Dupont")]
    public void BuildAttendanceCsv_EscapesNameWhenNeeded(string fullName, string expectedField)
    {
        var rows = new[]
        {
            (Date: new DateOnly(2026, 7, 26), FullName: fullName, CheckedInAt: new DateTime(2026, 7, 26, 9, 0, 0))
        };

        var csv = CsvExportService.BuildAttendanceCsv(rows);

        Assert.Equal($"Date,Nom,HeureArrivee{NL}2026-07-26,{expectedField},2026-07-26 09:00{NL}", csv);
    }
}
