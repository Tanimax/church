
namespace ChurchAttendance.Tests.Identity;

// Domain rules on User, no database involved.
public class UserTests
{
    [Theory]
    [InlineData("  Admin  ", "admin")]
    [InlineData("SECRETAIRE1", "secretaire1")]
    [InlineData("MixedCase", "mixedcase")]
    public void NormalizeUsername_TrimsAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, User.NormalizeUsername(input));
    }

    [Fact]
    public void Create_NormalizesUsernameAndStartsActive()
    {
        var createdAt = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

        var user = User.Create("  Marie.Paul ", "hash", UserRole.EcoleDominicale, createdAt);

        Assert.Equal("marie.paul", user.Username);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(UserRole.EcoleDominicale, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal(createdAt, user.CreatedAt);
    }

    [Fact]
    public void Rename_NormalizesLikeCreate()
    {
        var user = User.Create("marie", "hash", UserRole.Secretaire, DateTime.UtcNow);

        user.Rename(" PAUL ");

        Assert.Equal("paul", user.Username);
    }

    [Fact]
    public void Deactivate_ThenActivate_TogglesIsActive()
    {
        var user = User.Create("marie", "hash", UserRole.Secretaire, DateTime.UtcNow);

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Activate();
        Assert.True(user.IsActive);
    }
}
