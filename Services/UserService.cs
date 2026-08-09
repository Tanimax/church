using ChurchAttendance.Models;
using Microsoft.AspNetCore.Identity;

namespace ChurchAttendance.Services;

public static class UserService
{
    // The hasher doesn't actually read anything off the TUser instance in the default
    // implementation — it's only there for extensibility — so a null user is safe here.
    private static readonly PasswordHasher<User> Hasher = new();

    public static string HashPassword(string password) =>
        Hasher.HashPassword(null!, password);

    public static bool VerifyPassword(string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(null!, passwordHash, password) is
            PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();
}
