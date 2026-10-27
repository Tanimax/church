namespace ChurchAttendance.SharedKernel;

// The app's one Postgres connection string, resolved once by the host (DATABASE_URL on Fly,
// ConnectionStrings:Default elsewhere) and shared with every module's DbContext.
public sealed record DatabaseConnection(string ConnectionString);
