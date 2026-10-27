using ChurchAttendance.Data;
using ChurchAttendance.Features.SundayClasses;
using ChurchAttendance.Models;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Tests.Services;

public class SundayClassServiceTests
{
    private static SundayClassService CreateService(CustomWebApplicationFactory factory) =>
        new(factory.Services.GetRequiredService<IDbContextFactory<AppDbContext>>());

    private static async Task<Member> AddMemberAsync(CustomWebApplicationFactory factory, string firstName)
    {
        await using var db = factory.CreateDbContext();
        var member = new Member { FirstName = firstName, LastName = "Test", Token = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    [Fact]
    public async Task CreateClassAsync_TrimsName_AndClassesAreSortedByName()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);

        await service.CreateClassAsync("  Jeunes  ");
        await service.CreateClassAsync("Adultes");

        var classes = await service.GetClassesAsync();
        Assert.Equal(["Adultes", "Jeunes"], classes.Select(c => c.Name));
    }

    [Fact]
    public async Task AssignMemberAsync_MovesMemberBetweenClasses_AndUpdatesCounts()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        await service.CreateClassAsync("Adultes");
        await service.CreateClassAsync("Jeunes");
        var classes = await service.GetClassesAsync();
        var adultes = classes.Single(c => c.Name == "Adultes");
        var jeunes = classes.Single(c => c.Name == "Jeunes");
        var alice = await AddMemberAsync(factory, "Alice");
        var bob = await AddMemberAsync(factory, "Bob");

        await service.AssignMemberAsync(alice.Id, adultes.Id);
        await service.AssignMemberAsync(bob.Id, adultes.Id);
        await service.AssignMemberAsync(bob.Id, jeunes.Id);

        var counts = await service.GetMemberCountsAsync();
        Assert.Equal(1, counts[adultes.Id]);
        Assert.Equal(1, counts[jeunes.Id]);
    }

    [Fact]
    public async Task AssignMemberAsync_WithNull_RemovesMemberFromClass()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        await service.CreateClassAsync("Adultes");
        var adultes = (await service.GetClassesAsync()).Single();
        var alice = await AddMemberAsync(factory, "Alice");
        await service.AssignMemberAsync(alice.Id, adultes.Id);

        await service.AssignMemberAsync(alice.Id, null);

        await using var db = factory.CreateDbContext();
        Assert.Null((await db.Members.SingleAsync(m => m.Id == alice.Id)).SundayClassId);
        Assert.Empty(await service.GetMemberCountsAsync());
    }
}
