using System.ComponentModel.DataAnnotations;
using ChurchAttendance.Common;
using ChurchAttendance.Data;
using ChurchAttendance.Models;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Features.Members;

public record CreateMemberRequest(string FirstName, string LastName, string? Phone);

public record MemberResponse(int Id, string FirstName, string LastName, string FullName, string? Phone, string Token, string CardUrl);

public static class MembersEndpoints
{
    public static void MapMembersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/members");

        // Goes through MemberService (the same path as the Membres page) so the API applies the
        // same validation, normalization and welcome email instead of a second, divergent copy.
        group.MapPost("/", async (CreateMemberRequest request, MemberService memberService, HttpRequest httpRequest) =>
        {
            var form = new MemberFormModel
            {
                FirstName = request.FirstName ?? "",
                LastName = request.LastName ?? "",
                Phone = request.Phone
            };

            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(form, new ValidationContext(form), validationResults, validateAllProperties: true))
            {
                return Results.BadRequest(new { error = string.Join(" ", validationResults.Select(r => r.ErrorMessage)) });
            }

            var baseUrl = PublicUrls.BaseUrl(httpRequest);
            var member = await memberService.CreateMemberAsync(form, baseUrl);
            var response = new MemberResponse(member.Id, member.FirstName, member.LastName, member.FullName, member.Phone, member.Token, PublicUrls.Card(baseUrl, member.Token));

            return Results.Created($"/api/members/{member.Id}", response);
        });

        group.MapGet("/{token}/qr.png", async (string token, AppDbContext db, HttpRequest httpRequest) =>
        {
            var member = await db.Members.FirstOrDefaultAsync(m => m.Token == token && m.IsActive);
            if (member is null)
            {
                return Results.NotFound();
            }

            var png = QrCodeService.GeneratePng(PublicUrls.Card(httpRequest, token));

            return Results.File(png, "image/png");
        });

        app.MapGet("/admin/members/export.csv", async (string? search, string? baptized, string? active, AppDbContext db, ChurchClock clock) =>
        {
            var query = db.Members.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m => EF.Functions.ILike(m.FirstName + " " + m.LastName, $"%{search}%"));
            }

            // Bound as strings, not bool?: the export link always includes baptized=<value> and
            // active=<value> (even "" when no filter is selected), and ASP.NET Core's automatic
            // bool? query binder throws BadHttpRequestException on an empty string instead of
            // treating it as absent.
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

            var activeFilter = active switch
            {
                "true" => true,
                "false" => false,
                _ => (bool?)null
            };
            if (activeFilter is not null)
            {
                query = query.Where(m => m.IsActive == activeFilter);
            }

            var members = await query
                .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
                .ToListAsync();

            var csv = CsvExportService.BuildMembersCsv(members.Select(m =>
                (m.LastName, m.FirstName, m.Phone, m.BirthdayLabel, m.IsBaptized, m.IsActive)));
            var bytes = CsvExportService.ToCsvFileBytes(csv);

            return Results.File(bytes, "text/csv", $"membres_{clock.Today:yyyyMMdd}.csv");
        }).RequireAuthorization();

        app.MapGet("/admin/members/cards.zip", async (int[] ids, AppDbContext db, HttpRequest httpRequest, IWebHostEnvironment env, ChurchClock clock) =>
        {
            if (ids.Length == 0)
            {
                return Results.BadRequest(new { error = "Aucun membre sélectionné." });
            }

            var members = await db.Members
                .Where(m => ids.Contains(m.Id))
                .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
                .ToListAsync();

            var baseUrl = PublicUrls.BaseUrl(httpRequest);
            var zip = MemberCardZipService.BuildZip(
                members.Select(m => (m.FullName, PublicUrls.Card(baseUrl, m.Token))),
                env.WebRootPath);

            return Results.File(zip, "application/zip", $"cartes_{clock.Today:yyyyMMdd}.zip");
        }).RequireAuthorization();
    }
}
