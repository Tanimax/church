using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Errors;
using ChurchAttendance.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ChurchAttendance.Infrastructure.Persistence;

// The application's only DbContext: every table, one migration history.
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Member> Members => Set<Member>();
    public DbSet<ServiceSession> ServiceSessions => Set<ServiceSession>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Visitor> Visitors => Set<Visitor>();
    public DbSet<User> Users => Set<User>();
    public DbSet<SundayClass> SundayClasses => Set<SundayClass>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Member>()
            .HasIndex(m => m.Token)
            .IsUnique();

        modelBuilder.Entity<ServiceSession>()
            .HasIndex(s => s.Date)
            .IsUnique();

        modelBuilder.Entity<Attendance>()
            .HasIndex(a => new { a.MemberId, a.ServiceSessionId, a.Type })
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();
    }

    // A unique-constraint violation becomes Application's ConflictException, so use cases can
    // react to "someone saved the same thing meanwhile" without knowing about PostgreSQL.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new ConflictException(pg.ConstraintName, ex);
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new ConflictException(pg.ConstraintName, ex);
        }
    }
}

// Exposes EF Core's IDbContextFactory<AppDbContext> as Application's IAppDbContextFactory.
internal sealed class AppDbContextFactory(IDbContextFactory<AppDbContext> factory) : IAppDbContextFactory
{
    public async Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        await factory.CreateDbContextAsync(cancellationToken);
}
