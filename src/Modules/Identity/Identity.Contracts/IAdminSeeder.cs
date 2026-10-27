namespace ChurchAttendance.Identity.Contracts;

// Called by the host at startup so it can bootstrap the first account without knowing how
// users are stored or how passwords are hashed.
public interface IAdminSeeder
{
    // Creates an "admin" account with this password, but only if no account exists yet.
    Task SeedAdminIfNoUsersAsync(string password, CancellationToken cancellationToken = default);
}
