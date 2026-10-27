namespace ChurchAttendance.Identity.Contracts;

// Public because other features check roles by name, e.g. IsInRole(nameof(UserRole.Admin)):
// the names are the role claims the login endpoint issues.
public enum UserRole
{
    Secretaire = 0,
    Admin = 1,
    EcoleDominicale = 2
}
