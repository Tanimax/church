using ChurchAttendance.Infrastructure.Security;

namespace ChurchAttendance.Tests.Services;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.HashPassword("correct-horse-battery-staple");

        Assert.True(_hasher.VerifyPassword(hash, "correct-horse-battery-staple"));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("correct-horse-battery-staple");

        Assert.False(_hasher.VerifyPassword(hash, "wrong-password"));
    }

    [Fact]
    public void HashPassword_SamePasswordTwice_ProducesDifferentHashes()
    {
        var hash1 = _hasher.HashPassword("same-password");
        var hash2 = _hasher.HashPassword("same-password");

        // Salted — must never be equal even for identical input.
        Assert.NotEqual(hash1, hash2);
        Assert.True(_hasher.VerifyPassword(hash1, "same-password"));
        Assert.True(_hasher.VerifyPassword(hash2, "same-password"));
    }
}
