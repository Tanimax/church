using ChurchAttendance.SharedKernel.Results;

namespace ChurchAttendance.Application.SundayClasses;

// The expected ways a Sunday class use case can fail. Messages are shown to the user as is.
public static class SundayClassErrors
{
    public static readonly Error NameTaken = new("sunday_class.name_taken", "Une classe porte déjà ce nom.");
}
