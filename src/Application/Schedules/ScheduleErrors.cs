using ChurchAttendance.SharedKernel.Results;

namespace ChurchAttendance.Application.Schedules;

// The expected ways a schedule use case can fail. Messages are shown to the user as is.
public static class ScheduleErrors
{
    public static readonly Error WrongSundays = new("schedule.wrong_sundays",
        "Les dimanches ne correspondent pas à ce trimestre. Rechargez la page.");
    public static readonly Error NameTooLong = new("schedule.name_too_long",
        $"Un nom ne peut pas dépasser {ScheduleService.MaxNameLength} caractères.");
    public static readonly Error FooterTooLong = new("schedule.footer_too_long",
        $"La note de bas de page ne peut pas dépasser {ScheduleService.MaxFooterLength} caractères.");
    public static readonly Error SavedMeanwhile = new("schedule.saved_meanwhile",
        "Ce trimestre vient d'être enregistré par quelqu'un d'autre. Rechargez la page pour voir sa version.");
}
