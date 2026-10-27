using ChurchAttendance.Identity.Infrastructure;

namespace ChurchAttendance.Tests.Services;

public class PasswordHashingTests
{
    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var hash = PasswordHashing.HashPassword("correct-horse-battery-staple");

        Assert.True(PasswordHashing.VerifyPassword(hash, "correct-horse-battery-staple"));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordHashing.HashPassword("correct-horse-battery-staple");

        Assert.False(PasswordHashing.VerifyPassword(hash, "wrong-password"));
    }

    [Fact]
    public void HashPassword_SamePasswordTwice_ProducesDifferentHashes()
    {
        var hash1 = PasswordHashing.HashPassword("same-password");
        var hash2 = PasswordHashing.HashPassword("same-password");

        // Salted — must never be equal even for identical input.
        Assert.NotEqual(hash1, hash2);
        Assert.True(PasswordHashing.VerifyPassword(hash1, "same-password"));
        Assert.True(PasswordHashing.VerifyPassword(hash2, "same-password"));
    }
}
