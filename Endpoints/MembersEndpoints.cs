using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Endpoints;

public record CreateMemberRequest(string FirstName, string LastName, string? Phone);

public record MemberResponse(int Id, string FirstName, string LastName, string FullName, string? Phone, string Token, string CardUrl);

public static class MembersEndpoints
{
    public static void MapMembersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/members");

        group.MapPost("/", async (CreateMemberRequest request, AppDbContext db, HttpRequest httpRequest) =>
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return Results.BadRequest(new { error = "FirstName et LastName sont requis." });
            }

            var member = new Member
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Phone = request.Phone?.Trim(),
                Token = TokenService.GenerateToken(),
                CreatedAt = DateTime.UtcNow
            };

            db.Members.Add(member);
            await db.SaveChangesAsync();

            var baseUrl = $"{httpRequest.Scheme}://{httpRequest.Host}";
            var response = new MemberResponse(member.Id, member.FirstName, member.LastName, member.FullName, member.Phone, member.Token, $"{baseUrl}/card/{member.Token}");

            return Results.Created($"/api/members/{member.Id}", response);
        });

        group.MapGet("/{token}/qr.png", async (string token, AppDbContext db, HttpRequest httpRequest) =>
        {
            var member = await db.Members.FirstOrDefaultAsync(m => m.Token == token && m.IsActive);
            if (member is null)
            {
                return Results.NotFound();
            }

            var baseUrl = $"{httpRequest.Scheme}://{httpRequest.Host}";
            var cardUrl = $"{baseUrl}/card/{token}";
            var png = QrCodeService.GeneratePng(cardUrl);

            return Results.File(png, "image/png");
        });

        app.MapGet("/admin/members/export.csv", async (string? search, string? baptized, AppDbContext db) =>
        {
            var query = db.Members.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m => EF.Functions.ILike(m.FirstName + " " + m.LastName, $"%{search}%"));
            }

            // Bound as a string, not bool?: the export link always includes baptized=<value> (even
            // "" when no filter is selected), and ASP.NET Core's automatic bool? query binder throws
            // BadHttpRequestException on an empty string instead of treating it as absent.
            var baptizedFilter = baptized switch
            {
                "true" => true,
                "false" => false,
                _ => (bool?)null
            };
            if (baptizedFilter is not null)
            {
                query = query.Where(m => m.IsBaptized == baptizedFilter);
            }

            var members = await query
                .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
                .ToListAsync();

            var csv = CsvExportService.BuildMembersCsv(members.Select(m =>
                (m.LastName, m.FirstName, m.Phone, m.BirthdayLabel, m.IsBaptized, m.IsActive)));
            var bytes = CsvExportService.ToCsvFileBytes(csv);

            return Results.File(bytes, "text/csv", $"membres_{DateTime.Now:yyyyMMdd}.csv");
        }).RequireAuthorization();
    }
}
