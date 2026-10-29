using ChurchAttendance.SharedKernel.Results;

namespace ChurchAttendance.Application.Announcements;

// The expected ways an announcement use case can fail. Messages are shown to the user as is.
public static class AnnouncementErrors
{
    public static readonly Error NotFound = new("announcement.not_found",
        "Cette annonce n'existe plus. Rechargez la page.");
    public static readonly Error TitleRequired = new("announcement.title_required",
        "Indiquez le titre de l'annonce (ce qui a lieu).");
    public static readonly Error ScheduleRequired = new("announcement.schedule_required",
        "Indiquez quand l'activité a lieu (par exemple « Mercredi soir de 19h00 à 21h15 »).");
    public static readonly Error EventDateRequired = new("announcement.event_date_required",
        "Indiquez la date de l'événement : c'est elle qui retire l'annonce une fois passée.");
    public static readonly Error EndDateWithoutStart = new("announcement.end_date_without_start",
        "Indiquez aussi la date de début.");
    public static readonly Error EndBeforeStart = new("announcement.end_before_start",
        "La date de fin ne peut pas être avant la date de début.");
    public static readonly Error TooManyDetails = new("announcement.too_many_details",
        $"Une annonce ne peut pas avoir plus de {AnnouncementService.MaxDetails} lignes de détail.");
    public static readonly Error TooLong = new("announcement.too_long",
        "Un des champs est trop long. Raccourcissez-le : l'annonce doit tenir sur une diapositive.");
    public static readonly Error ThemeTooLong = new("announcement.theme_too_long",
        $"Le thème du mois ne peut pas dépasser {AnnouncementService.MaxThemeLength} caractères.");
}
