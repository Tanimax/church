using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Domain;
using Microsoft.EntityFrameworkCore;

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
}

// Exposes EF Core's IDbContextFactory<AppDbContext> as Application's IAppDbContextFactory.
internal sealed class AppDbContextFactory(IDbContextFactory<AppDbContext> factory) : IAppDbContextFactory
{
    public async Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        await factory.CreateDbContextAsync(cancellationToken);
}
