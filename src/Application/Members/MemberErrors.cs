using ChurchAttendance.SharedKernel.Results;

namespace ChurchAttendance.Application.Members;

// The expected ways a member use case can fail. Messages are shown to the user as is.
public static class MemberErrors
{
    public static readonly Error NotFound = new("member.not_found", "Ce membre n'existe plus.");
}
