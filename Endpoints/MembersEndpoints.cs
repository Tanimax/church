using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Endpoints;

public record CreateMemberRequest(string FullName, string? Phone);

public record MemberResponse(int Id, string FullName, string? Phone, string Token, string CardUrl);

public static class MembersEndpoints
{
    public static void MapMembersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/members");

        group.MapPost("/", async (CreateMemberRequest request, AppDbContext db, HttpRequest httpRequest) =>
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                return Results.BadRequest(new { error = "FullName est requis." });
            }

            var member = new Member
            {
                FullName = request.FullName.Trim(),
                Phone = request.Phone?.Trim(),
                Token = TokenService.GenerateToken(),
                CreatedAt = DateTime.UtcNow
            };

            db.Members.Add(member);
            await db.SaveChangesAsync();

            var baseUrl = $"{httpRequest.Scheme}://{httpRequest.Host}";
            var response = new MemberResponse(member.Id, member.FullName, member.Phone, member.Token, $"{baseUrl}/card/{member.Token}");

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
    }
}
