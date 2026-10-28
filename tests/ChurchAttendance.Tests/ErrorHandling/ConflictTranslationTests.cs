using ChurchAttendance.Application.Errors;
using ChurchAttendance.Tests.Infrastructure;

namespace ChurchAttendance.Tests.ErrorHandling;

public class ConflictTranslationTests
{
    [Fact]
    public async Task SaveChanges_UniqueViolation_BecomesConflictExceptionWithTheConstraintName()
    {
        using var factory = new CustomWebApplicationFactory();
        await factory.SeedUserAsync("marie", "motdepasse123", UserRole.Secretaire);
        await using var db = factory.CreateDbContext();
        db.Users.Add(User.Create("MARIE", "hash", UserRole.Secretaire, DateTime.UtcNow));

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesAsync());

        Assert.Equal("IX_Users_Username", conflict.ConstraintName);
    }
}
