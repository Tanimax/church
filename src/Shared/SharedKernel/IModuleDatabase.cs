namespace ChurchAttendance.SharedKernel;

// A module's own database migrations. The host runs every registered IModuleDatabase at
// startup, in ModulesSetup order, after the shared AppDbContext's migrations.
public interface IModuleDatabase
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
