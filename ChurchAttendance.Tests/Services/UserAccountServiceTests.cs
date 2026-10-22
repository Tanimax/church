using ChurchAttendance.Data;
using ChurchAttendance.Features.Users;
using ChurchAttendance.Models;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Tests.Services;

public class UserAccountServiceTests
{
    private static UserAccountService CreateService(CustomWebApplicationFactory factory) =>
        new(factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>());

    [Fact]
    public async Task CreateUserAsync_NormalizesUsernameAndHashesPassword()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);

        var result = await service.CreateUserAsync("  Marie.Paul ", "motdepasse123", UserRole.Secretaire);

        Assert.Equal(UserSaveResult.Saved, result);
        var user = (await service.GetUsersAsync()).Single(u => u.Username == "marie.paul");
        Assert.Equal(UserRole.Secretaire, user.Role);
        Assert.True(UserService.VerifyPassword(user.PasswordHash, "motdepasse123"));
    }

    [Fact]
    public async Task CreateUserAsync_UsernameTakenIgnoringCase_ReturnsUsernameTaken()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        await service.CreateUserAsync("marie", "motdepasse123", UserRole.Secretaire);

        var result = await service.CreateUserAsync("MARIE", "autrepasse123", UserRole.Admin);

        Assert.Equal(UserSaveResult.UsernameTaken, result);
    }

    [Fact]
    public async Task UpdateUserAsync_EmptyPassword_KeepsCurrentPassword()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        await service.CreateUserAsync("marie", "motdepasse123", UserRole.Secretaire);
        var marie = (await service.GetUsersAsync()).Single(u => u.Username == "marie");

        var result = await service.UpdateUserAsync(marie.Id, "marie", UserRole.EcoleDominicale, isActive: false, newPassword: "");

        Assert.Equal(UserSaveResult.Saved, result);
        var updated = (await service.GetUsersAsync()).Single(u => u.Id == marie.Id);
        Assert.Equal(UserRole.EcoleDominicale, updated.Role);
        Assert.False(updated.IsActive);
        Assert.True(UserService.VerifyPassword(updated.PasswordHash, "motdepasse123"));
    }

    [Fact]
    public async Task UpdateUserAsync_RenameToAnotherUsersName_ReturnsUsernameTaken()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        await service.CreateUserAsync("marie", "motdepasse123", UserRole.Secretaire);
        await service.CreateUserAsync("paul", "motdepasse123", UserRole.Secretaire);
        var paul = (await service.GetUsersAsync()).Single(u => u.Username == "paul");

        var result = await service.UpdateUserAsync(paul.Id, "Marie", UserRole.Secretaire, isActive: true, newPassword: null);

        Assert.Equal(UserSaveResult.UsernameTaken, result);
    }

    [Fact]
    public async Task UpdateUserAsync_UnknownUser_ReturnsNotFound()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);

        var result = await service.UpdateUserAsync(999_999, "fantome", UserRole.Secretaire, isActive: true, newPassword: null);

        Assert.Equal(UserSaveResult.NotFound, result);
    }
}
