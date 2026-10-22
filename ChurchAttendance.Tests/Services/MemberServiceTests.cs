using ChurchAttendance.Features.Members;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
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
    public async Task UpdateMemberAsync_UpdatesDetails_AndReturnsFalseForUnknownMember()
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

        Assert.True(updated);
        Assert.False(unknown);
        var saved = (await service.GetMembersAsync()).Single(m => m.Id == member.Id);
        Assert.Equal("Jean-Marc", saved.FirstName);
        Assert.Equal("514-555-0101", saved.Phone);
        Assert.Equal(member.Token, saved.Token);
    }

    [Fact]
    public async Task ToggleActiveAsync_FlipsStatus_AndReturnsNullForUnknownMember()
    {
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemberService>();
        var member = await service.CreateMemberAsync(new MemberFormModel { FirstName = "Jean", LastName = "Dupont" }, BaseUri);

        Assert.False(await service.ToggleActiveAsync(member.Id));
        Assert.True(await service.ToggleActiveAsync(member.Id));
        Assert.Null(await service.ToggleActiveAsync(999_999));
    }

    [Fact]
    public void CardUrl_JoinsBaseUriAndTokenWithSingleSlash()
    {
        Assert.Equal("https://eglise.example/card/abc", MemberService.CardUrl("https://eglise.example/", "abc"));
        Assert.Equal("https://eglise.example/card/abc", MemberService.CardUrl("https://eglise.example", "abc"));
    }
}
