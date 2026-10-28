using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.Users;

public enum UserSaveResult
{
    Saved,
    UsernameTaken,
    NotFound
}

// Every use case on accounts: the Utilisateurs page, the password change button, the login
// endpoint and the startup seed all go through here. Uses IAppDbContextFactory because most
// callers are Blazor Server components on long-lived circuits.
public class UserAccountService(IAppDbContextFactory dbFactory, IPasswordHasher passwordHasher)
{
    public async Task<List<User>> GetUsersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.OrderBy(u => u.Username).ToListAsync();
    }

    // The active account matching these credentials, or null.
    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var normalizedUsername = User.NormalizeUsername(username);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == normalizedUsername && u.IsActive);
        return user is not null && passwordHasher.VerifyPassword(user.PasswordHash, password) ? user : null;
    }

    public async Task<UserSaveResult> CreateUserAsync(string username, string password, UserRole role)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var user = User.Create(username, passwordHasher.HashPassword(password), role, DateTime.UtcNow);
        if (await db.Users.AnyAsync(u => u.Username == user.Username))
        {
            return UserSaveResult.UsernameTaken;
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return UserSaveResult.Saved;
    }

    // An empty newPassword keeps the current password.
    public async Task<UserSaveResult> UpdateUserAsync(int userId, string username, UserRole role, bool isActive, string? newPassword)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user is null)
        {
            return UserSaveResult.NotFound;
        }

        var normalizedUsername = User.NormalizeUsername(username);
        if (await db.Users.AnyAsync(u => u.Id != userId && u.Username == normalizedUsername))
        {
            return UserSaveResult.UsernameTaken;
        }

        user.Rename(username);
        user.ChangeRole(role);
        if (isActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }
        if (!string.IsNullOrEmpty(newPassword))
        {
            user.ChangePasswordHash(passwordHasher.HashPassword(newPassword));
        }

        await db.SaveChangesAsync();
        return UserSaveResult.Saved;
    }

    // False when there is no such account or currentPassword is wrong.
    public async Task<bool> ChangePasswordAsync(string username, string currentPassword, string newPassword)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !passwordHasher.VerifyPassword(user.PasswordHash, currentPassword))
        {
            return false;
        }

        user.ChangePasswordHash(passwordHasher.HashPassword(newPassword));
        await db.SaveChangesAsync();
        return true;
    }

    // One-time bootstrap: the app used to gate the whole admin area behind a single shared
    // ADMIN_PASSWORD. The first time this runs against a database with no user accounts yet,
    // it creates an "admin" account with that same password so existing deployments keep
    // working without any manual step.
    public async Task SeedAdminIfNoUsersAsync(string password)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Users.AnyAsync())
        {
            return;
        }

        db.Users.Add(User.Create("admin", passwordHasher.HashPassword(password), UserRole.Admin, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }
}
