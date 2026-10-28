using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Domain;
using Microsoft.AspNetCore.Identity;

namespace ChurchAttendance.Infrastructure.Security;

// ASP.NET Core Identity's hasher (PBKDF2, salted), without the rest of Identity.
public sealed class PasswordHasher : IPasswordHasher
{
    // The hasher doesn't actually read anything off the TUser instance in the default
    // implementation — it's only there for extensibility — so a null user is safe here.
    private static readonly PasswordHasher<User> Hasher = new();

    public string HashPassword(string password) =>
        Hasher.HashPassword(null!, password);

    public bool VerifyPassword(string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(null!, passwordHash, password) is
            PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
