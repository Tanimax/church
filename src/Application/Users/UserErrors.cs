using ChurchAttendance.SharedKernel.Results;

namespace ChurchAttendance.Application.Users;

// The expected ways an account use case can fail. Messages are shown to the user as is.
public static class UserErrors
{
    public static readonly Error UsernameTaken = new("user.username_taken", "Ce nom d'utilisateur existe déjà.");
    public static readonly Error NotFound = new("user.not_found", "Cet utilisateur n'existe plus.");
    public static readonly Error WrongCurrentPassword = new("user.wrong_password", "Le mot de passe actuel est incorrect.");
}
