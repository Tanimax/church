using ChurchAttendance.Data;
using ChurchAttendance.Models;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Features.Users;

public enum UserSaveResult
{
    Saved,
    UsernameTaken,
    NotFound
}

// Account management for the Utilisateurs page. Uses IDbContextFactory because its callers
// are Blazor Server components on long-lived circuits.
public class UserAccountService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<User>> GetUsersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.OrderBy(u => u.Username).ToListAsync();
    }

    public async Task<UserSaveResult> CreateUserAsync(string username, string password, UserRole role)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var normalizedUsername = UserService.NormalizeUsername(username);
        if (await db.Users.AnyAsync(u => u.Username == normalizedUsername))
        {
            return UserSaveResult.UsernameTaken;
        }

        db.Users.Add(new User
        {
            Username = normalizedUsername,
            PasswordHash = UserService.HashPassword(password),
            Role = role,
            CreatedAt = DateTime.UtcNow
        });
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

        var normalizedUsername = UserService.NormalizeUsername(username);
        if (await db.Users.AnyAsync(u => u.Id != userId && u.Username == normalizedUsername))
        {
            return UserSaveResult.UsernameTaken;
        }

        user.Username = normalizedUsername;
        user.Role = role;
        user.IsActive = isActive;
        if (!string.IsNullOrEmpty(newPassword))
        {
            user.PasswordHash = UserService.HashPassword(newPassword);
        }

        await db.SaveChangesAsync();
        return UserSaveResult.Saved;
    }
}
