using System.Text;
using ChurchAttendance.Common;
using ChurchAttendance.Data;

namespace ChurchAttendance.Features.Reports;

public static class ReportsEndpoints
{
    public static void MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/reports/export.csv", async (DateOnly? start, DateOnly? end, AppDbContext db, ChurchClock clock) =>
        {
            var rangeStart = start ?? clock.Today.AddDays(-56);
            var rangeEnd = end ?? clock.Today;

            var presences = await AttendanceReportService.GetPresencesAsync(db, rangeStart, rangeEnd);

            var csv = CsvExportService.BuildAttendanceCsv(presences.Select(p => (p.Date, p.FullName, clock.ToLocal(p.CheckedInAt))));
            var bytes = Encoding.UTF8.GetBytes(csv);
            return Results.File(bytes, "text/csv", $"presences_{rangeStart:yyyyMMdd}_{rangeEnd:yyyyMMdd}.csv");
        }).RequireAuthorization();
    }
}
