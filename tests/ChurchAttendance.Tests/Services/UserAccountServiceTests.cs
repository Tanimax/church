using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Users;
using ChurchAttendance.Infrastructure.Persistence;
using ChurchAttendance.Infrastructure.Security;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Tests.Services;

public class UserAccountServiceTests
{
    private static readonly PasswordHasher Hasher = new();

    private static UserAccountService CreateService(CustomWebApplicationFactory factory) =>
        new(factory.Services.GetRequiredService<IAppDbContextFactory>(), Hasher);

    [Fact]
    public async Task CreateUserAsync_NormalizesUsernameAndHashesPassword()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);

        var result = await service.CreateUserAsync("  Marie.Paul ", "motdepasse123", UserRole.Secretaire);

        Assert.True(result.IsSuccess);
        var user = (await service.GetUsersAsync()).Single(u => u.Username == "marie.paul");
        Assert.Equal(UserRole.Secretaire, user.Role);
        Assert.True(Hasher.VerifyPassword(user.PasswordHash, "motdepasse123"));
    }

    [Fact]
    public async Task CreateUserAsync_UsernameTakenIgnoringCase_ReturnsUsernameTaken()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        await service.CreateUserAsync("marie", "motdepasse123", UserRole.Secretaire);

        var result = await service.CreateUserAsync("MARIE", "autrepasse123", UserRole.Admin);

        Assert.Equal(UserErrors.UsernameTaken, result.Error);
    }

    [Fact]
    public async Task UpdateUserAsync_EmptyPassword_KeepsCurrentPassword()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        await service.CreateUserAsync("marie", "motdepasse123", UserRole.Secretaire);
        var marie = (await service.GetUsersAsync()).Single(u => u.Username == "marie");

        var result = await service.UpdateUserAsync(marie.Id, "marie", UserRole.EcoleDominicale, isActive: false, newPassword: "");

        Assert.True(result.IsSuccess);
        var updated = (await service.GetUsersAsync()).Single(u => u.Id == marie.Id);
        Assert.Equal(UserRole.EcoleDominicale, updated.Role);
        Assert.False(updated.IsActive);
        Assert.True(Hasher.VerifyPassword(updated.PasswordHash, "motdepasse123"));
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

        Assert.Equal(UserErrors.UsernameTaken, result.Error);
    }

    [Fact]
    public async Task UpdateUserAsync_UnknownUser_ReturnsNotFound()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);

        var result = await service.UpdateUserAsync(999_999, "fantome", UserRole.Secretaire, isActive: true, newPassword: null);

        Assert.Equal(UserErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsActiveUserForCorrectCredentialsOnly()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        await service.CreateUserAsync("marie", "motdepasse123", UserRole.Secretaire);
        await factory.SeedUserAsync("paul", "motdepasse123", UserRole.Secretaire, isActive: false);

        Assert.Equal("marie", (await service.AuthenticateAsync(" MARIE ", "motdepasse123"))?.Username);
        Assert.Null(await service.AuthenticateAsync("marie", "mauvais"));
        Assert.Null(await service.AuthenticateAsync("paul", "motdepasse123"));
        Assert.Null(await service.AuthenticateAsync("inconnu", "motdepasse123"));
    }

    [Fact]
    public async Task ChangePasswordAsync_RequiresTheCurrentPassword()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        await service.CreateUserAsync("marie", "motdepasse123", UserRole.Secretaire);

        Assert.Equal(UserErrors.WrongCurrentPassword, (await service.ChangePasswordAsync("marie", "mauvais", "nouveaupasse123")).Error);
        Assert.True((await service.ChangePasswordAsync("marie", "motdepasse123", "nouveaupasse123")).IsSuccess);

        Assert.Null(await service.AuthenticateAsync("marie", "motdepasse123"));
        Assert.NotNull(await service.AuthenticateAsync("marie", "nouveaupasse123"));
    }

    // Simulates another admin saving the same username in the window between the service's
    // "already taken?" check and its own insert: the interceptor inserts it right before the
    // service's SaveChanges runs, so the unique index rejects the service's row.
    private sealed class SomeoneElseSavesFirst(string connectionString, string username) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await using var other = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
                other.Users.Add(User.Create(username, Hasher.HashPassword("autrepasse123"), UserRole.Secretaire, DateTime.UtcNow));
                await other.SaveChangesAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class InterceptedFactory(string connectionString, IInterceptor interceptor) : IAppDbContextFactory
    {
        public Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IAppDbContext>(new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString).AddInterceptors(interceptor).Options));
    }

    [Fact]
    public async Task CreateUserAsync_UsernameTakenBetweenCheckAndInsert_ReturnsUsernameTaken()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.Services; // starts the app, which migrates the test database
        var service = new UserAccountService(
            new InterceptedFactory(factory.ConnectionString, new SomeoneElseSavesFirst(factory.ConnectionString, "marie")), Hasher);

        var result = await service.CreateUserAsync("marie", "motdepasse123", UserRole.Secretaire);

        Assert.Equal(UserErrors.UsernameTaken, result.Error);
    }

    [Fact]
    public async Task UpdateUserAsync_UsernameTakenBetweenCheckAndUpdate_ReturnsUsernameTaken()
    {
        using var factory = new CustomWebApplicationFactory();
        await CreateService(factory).CreateUserAsync("paul", "motdepasse123", UserRole.Secretaire);
        var paul = (await CreateService(factory).GetUsersAsync()).Single(u => u.Username == "paul");
        var service = new UserAccountService(
            new InterceptedFactory(factory.ConnectionString, new SomeoneElseSavesFirst(factory.ConnectionString, "marie")), Hasher);

        var result = await service.UpdateUserAsync(paul.Id, "marie", UserRole.Secretaire, isActive: true, newPassword: null);

        Assert.Equal(UserErrors.UsernameTaken, result.Error);
    }
}
