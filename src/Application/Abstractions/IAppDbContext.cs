using ChurchAttendance.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.Abstractions;

// The database as the use cases see it: the tables and SaveChanges. Implemented by
// Infrastructure's AppDbContext, so nothing in Application knows about PostgreSQL.
public interface IAppDbContext : IDisposable, IAsyncDisposable
{
    DbSet<Member> Members { get; }
    DbSet<ServiceSession> ServiceSessions { get; }
    DbSet<Attendance> Attendances { get; }
    DbSet<Visitor> Visitors { get; }
    DbSet<User> Users { get; }
    DbSet<SundayClass> SundayClasses { get; }
    DbSet<ServiceSchedule> ServiceSchedules { get; }
    DbSet<ScheduledService> ScheduledServices { get; }
    DbSet<Announcement> Announcements { get; }
    DbSet<MonthlyTheme> MonthlyThemes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

// Blazor Server components live on long-lived circuits, so they (and the services they call)
// create a short-lived context per operation instead of sharing one scoped instance.
public interface IAppDbContextFactory
{
    Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
}
