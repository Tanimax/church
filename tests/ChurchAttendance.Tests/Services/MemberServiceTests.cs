using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Members;
using ChurchAttendance.Infrastructure.Persistence;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Tests.Services;

public class MemberServiceTests
{
    private const string BaseUri = "https://eglise.example/";

    [Fact]
    public async Task CreateMemberAsync_TrimsFields_BlankOptionalsBecomeNull_AndGeneratesToken()
    {
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemberService>();

        var member = await service.CreateMemberAsync(new MemberFormModel
        {
            FirstName = "  Jean ",
            LastName = " Dupont ",
            Phone = "   ",
            Email = "",
            IsBaptized = true,
            BirthDay = 4,
            BirthMonth = 10
        }, BaseUri);

        await using var db = factory.CreateDbContext();
        var saved = await db.Members.SingleAsync(m => m.Id == member.Id);
        Assert.Equal("Jean", saved.FirstName);
        Assert.Equal("Dupont", saved.LastName);
        Assert.Null(saved.Phone);
        Assert.Null(saved.Email);
        Assert.True(saved.IsBaptized);
        Assert.Equal((4, 10), (saved.BirthDay, saved.BirthMonth));
        Assert.False(string.IsNullOrWhiteSpace(saved.Token));
    }

    [Fact]
    public async Task UpdateMemberAsync_UpdatesDetails_AndReturnsNotFoundForUnknownMember()
    {
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemberService>();
        var member = await service.CreateMemberAsync(new MemberFormModel { FirstName = "Jean", LastName = "Dupont" }, BaseUri);

        var updated = await service.UpdateMemberAsync(member.Id, new MemberFormModel
        {
            FirstName = "Jean-Marc",
            LastName = "Dupont",
            Phone = " 514-555-0101 "
        });
        var unknown = await service.UpdateMemberAsync(999_999, new MemberFormModel { FirstName = "X", LastName = "Y" });

        Assert.True(updated.IsSuccess);
        Assert.Equal(MemberErrors.NotFound, unknown.Error);
        var saved = (await service.GetMembersAsync()).Single(m => m.Id == member.Id);
        Assert.Equal("Jean-Marc", saved.FirstName);
        Assert.Equal("514-555-0101", saved.Phone);
        Assert.Equal(member.Token, saved.Token);
    }

    [Fact]
    public async Task ToggleActiveAsync_FlipsStatus_AndReturnsNotFoundForUnknownMember()
    {
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemberService>();
        var member = await service.CreateMemberAsync(new MemberFormModel { FirstName = "Jean", LastName = "Dupont" }, BaseUri);

        Assert.False((await service.ToggleActiveAsync(member.Id)).Value);
        Assert.True((await service.ToggleActiveAsync(member.Id)).Value);
        Assert.Equal(MemberErrors.NotFound, (await service.ToggleActiveAsync(999_999)).Error);
    }

    [Fact]
    public async Task CreateMemberAsync_WithAnEmail_SendsTheWelcomeEmail_WithTheirCardLink()
    {
        using var factory = new CustomWebApplicationFactory();
        var email = new RecordingEmailSender();
        var service = new MemberService(factory.Services.GetRequiredService<IAppDbContextFactory>(), email,
            factory.Services.GetRequiredService<ChurchClock>());

        var member = await service.CreateMemberAsync(new MemberFormModel { FirstName = "Marie", LastName = "Dupont", Email = " marie@exemple.ca " }, BaseUri);
        await service.CreateMemberAsync(new MemberFormModel { FirstName = "Paul", LastName = "Joseph" }, BaseUri);

        var sent = Assert.Single(email.Sent);
        Assert.Equal(("marie@exemple.ca", "Marie Dupont", $"https://eglise.example/card/{member.Token}"), sent);
    }

    [Fact]
    public async Task CreateMemberAsync_CardTokenAlreadyTaken_DrawsAnotherOne()
    {
        using var factory = new CustomWebApplicationFactory();
        _ = factory.Services; // starts the app, which migrates the test database
        var interceptor = new TakesTheNewMembersToken(factory.ConnectionString);
        var service = new MemberService(new InterceptedFactory(factory.ConnectionString, interceptor), new RecordingEmailSender(),
            factory.Services.GetRequiredService<ChurchClock>());

        var member = await service.CreateMemberAsync(new MemberFormModel { FirstName = "Marie", LastName = "Dupont" }, BaseUri);

        Assert.NotNull(interceptor.StolenToken);
        Assert.NotEqual(interceptor.StolenToken, member.Token);
        await using var db = factory.CreateDbContext();
        Assert.Equal(member.Token, (await db.Members.SingleAsync(m => m.LastName == "Dupont")).Token);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string FullName, string CardUrl)> Sent { get; } = [];

        public Task SendWelcomeEmailAsync(string toEmail, string fullName, string cardUrl)
        {
            Sent.Add((toEmail, fullName, cardUrl));
            return Task.CompletedTask;
        }
    }

    // Right before the first save, another member is given the very token just drawn for the
    // new one, so the unique index rejects it: the collision a random token makes possible.
    private sealed class TakesTheNewMembersToken(string connectionString) : SaveChangesInterceptor
    {
        public string? StolenToken { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (StolenToken is null)
            {
                StolenToken = eventData.Context!.ChangeTracker.Entries<Member>().Single().Entity.Token;
                await using var other = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
                other.Members.Add(new Member { FirstName = "Autre", LastName = "Membre", Token = StolenToken, CreatedAt = DateTime.UtcNow });
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
}
