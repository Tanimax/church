using ChurchAttendance.Services;

namespace ChurchAttendance.Tests.Services;

public class UserServiceTests
{
    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var hash = UserService.HashPassword("correct-horse-battery-staple");

        Assert.True(UserService.VerifyPassword(hash, "correct-horse-battery-staple"));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = UserService.HashPassword("correct-horse-battery-staple");

        Assert.False(UserService.VerifyPassword(hash, "wrong-password"));
    }

    [Fact]
    public void HashPassword_SamePasswordTwice_ProducesDifferentHashes()
    {
        var hash1 = UserService.HashPassword("same-password");
        var hash2 = UserService.HashPassword("same-password");

        // Salted — must never be equal even for identical input.
        Assert.NotEqual(hash1, hash2);
        Assert.True(UserService.VerifyPassword(hash1, "same-password"));
        Assert.True(UserService.VerifyPassword(hash2, "same-password"));
    }

    [Theory]
    [InlineData("  Admin  ", "admin")]
    [InlineData("SECRETAIRE1", "secretaire1")]
    [InlineData("MixedCase", "mixedcase")]
    public void NormalizeUsername_TrimsAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, UserService.NormalizeUsername(input));
    }
}
