using System.Text;
using ChurchAttendance.Data;
using ChurchAttendance.Services;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Endpoints;

public static class ReportsEndpoints
{
    public static void MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/reports/export.csv", async (DateOnly? start, DateOnly? end, AppDbContext db) =>
        {
            var rangeStart = start ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-56));
            var rangeEnd = end ?? DateOnly.FromDateTime(DateTime.Today);

            var attendances = await db.Attendances
                .Include(a => a.Member)
                .Include(a => a.ServiceSession)
                .Where(a => a.ServiceSession!.Date >= rangeStart && a.ServiceSession!.Date <= rangeEnd)
                .ToListAsync();

            // A member can have both a Culte and a Sainte Cène record for the same Sunday —
            // for general attendance purposes that's a single presence, not two.
            var rows = attendances
                .GroupBy(a => (a.MemberId, a.ServiceSessionId))
                .Select(g => g.OrderBy(a => a.CheckedInAt).First())
                .OrderByDescending(a => a.ServiceSession!.Date)
                .ThenBy(a => a.Member!.FullName)
                .Select(a => new { a.ServiceSession!.Date, a.Member!.FullName, a.CheckedInAt });

            var csv = CsvExportService.BuildAttendanceCsv(rows.Select(r => (r.Date, r.FullName, r.CheckedInAt)));
            var bytes = Encoding.UTF8.GetBytes(csv);
            return Results.File(bytes, "text/csv", $"presences_{rangeStart:yyyyMMdd}_{rangeEnd:yyyyMMdd}.csv");
        }).RequireAuthorization();
    }
}
