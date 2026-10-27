using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Identity.Infrastructure;

// The Identity module's own view of the database. For now it maps the existing public.Users
// table as-is (the schema was created by AppDbContext's migrations); its own migration
// history lives in a separate table so the two contexts never see each other's migrations.
public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_Identity";

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("Users");
            user.HasIndex(u => u.Username).IsUnique();
        });
    }
}
