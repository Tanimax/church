using ChurchAttendance.Identity.Contracts;

namespace ChurchAttendance.Identity.Core;

// A login account. State only changes through the methods below, so every path that sets a
// username goes through the same normalization.
public class User
{
    // For EF Core.
    private User()
    {
    }

    public int Id { get; private set; }
    public string Username { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public UserRole Role { get; private set; } = UserRole.Secretaire;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    public static User Create(string username, string passwordHash, UserRole role, DateTime createdAtUtc) => new()
    {
        Username = NormalizeUsername(username),
        PasswordHash = passwordHash,
        Role = role,
        IsActive = true,
        CreatedAt = createdAtUtc
    };

    // Usernames are compared case-insensitively by storing them trimmed and lowercased.
    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public void Rename(string username) => Username = NormalizeUsername(username);

    public void ChangeRole(UserRole role) => Role = role;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void ChangePasswordHash(string passwordHash) => PasswordHash = passwordHash;
}
