namespace ChurchAttendance.Domain;

// The names are the role claims the login endpoint issues; pages check them by name,
// e.g. IsInRole(nameof(UserRole.Admin)).
public enum UserRole
{
    Secretaire = 0,
    Admin = 1,
    EcoleDominicale = 2
}
