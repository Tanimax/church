using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Announcements;
using ChurchAttendance.Application.Errors;
using ChurchAttendance.Application.Schedules;
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
    public DbSet<ServiceSchedule> ServiceSchedules => Set<ServiceSchedule>();
    public DbSet<ScheduledService> ScheduledServices => Set<ScheduledService>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<MonthlyTheme> MonthlyThemes => Set<MonthlyTheme>();

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

        modelBuilder.Entity<ServiceSchedule>(schedule =>
        {
            schedule.HasIndex(s => new { s.Year, s.Quarter }).IsUnique();
            schedule.Property(s => s.FooterNote).HasMaxLength(ScheduleService.MaxFooterLength);
            schedule.HasMany(s => s.Services).WithOne().HasForeignKey(s => s.ServiceScheduleId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ScheduledService>(service =>
        {
            service.HasIndex(s => new { s.ServiceScheduleId, s.Date }).IsUnique();
            service.Property(s => s.Leader).HasMaxLength(ScheduleService.MaxNameLength);
            service.Property(s => s.Preacher).HasMaxLength(ScheduleService.MaxNameLength);
        });
        modelBuilder.Entity<Announcement>(announcement =>
        {
            announcement.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);
            announcement.Property(a => a.Heading).HasMaxLength(AnnouncementService.MaxHeadingLength);
            announcement.Property(a => a.Schedule).HasMaxLength(AnnouncementService.MaxScheduleLength);
            announcement.Property(a => a.EventTime).HasMaxLength(AnnouncementService.MaxEventTimeLength);
            announcement.Property(a => a.Title).HasMaxLength(AnnouncementService.MaxTitleLength);
            // The detail lines only ever travel with their announcement: one JSON column.
            announcement.OwnsMany(a => a.Details, details => details.ToJson());
            announcement.Ignore(a => a.LastDay);
        });
        modelBuilder.Entity<MonthlyTheme>(theme =>
        {
            theme.HasIndex(t => new { t.Year, t.Month }).IsUnique();
            theme.Property(t => t.Text).HasMaxLength(AnnouncementService.MaxThemeLength);
        });
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
