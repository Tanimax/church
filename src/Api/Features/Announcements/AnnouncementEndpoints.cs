using System.Globalization;
using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Announcements;
using ChurchAttendance.Startup;

namespace ChurchAttendance.Features.Announcements;

public static class AnnouncementEndpoints
{
    public const string PowerPointContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    // The PowerPoint to project that Sunday ("/admin/annonces/2026-11-01/pptx").
    public static string DeckUrl(DateOnly sunday) => $"/admin/annonces/{sunday:yyyy-MM-dd}/pptx";

    public static void MapAnnouncementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/annonces/{sunday}/pptx",
            async (string sunday, AnnouncementService announcements, IAnnouncementSlidesRenderer renderer) =>
            {
                // Announcements are read on Sunday: any other day has no deck.
                if (!DateOnly.TryParseExact(sunday, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    || date.DayOfWeek != DayOfWeek.Sunday)
                {
                    return Results.NotFound();
                }
                var deck = await announcements.GetDeckAsync(date);
                var fileName = $"Annonces_du_{deck.Date.ToString().Replace("Dimanche", "dimanche").Replace(' ', '_')}.pptx";
                return Results.File(renderer.Render(deck), PowerPointContentType, fileName);
            }).RequireAuthorization(AuthorizationPolicies.ManageCommunication);
    }
}
