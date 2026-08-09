using ChurchAttendance.Models;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();
    public DbSet<ServiceSession> ServiceSessions => Set<ServiceSession>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Visitor> Visitors => Set<Visitor>();
    public DbSet<User> Users => Set<User>();

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
