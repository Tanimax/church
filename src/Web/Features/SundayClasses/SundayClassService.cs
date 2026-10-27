using ChurchAttendance.Data;
using ChurchAttendance.Models;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Features.SundayClasses;

// Uses IDbContextFactory (not a scoped DbContext) because its callers are Blazor Server
// components on long-lived circuits — each operation gets its own short-lived context.
public class SundayClassService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<SundayClass>> GetClassesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.SundayClasses.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Dictionary<int, int>> GetMemberCountsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Members
            .Where(m => m.SundayClassId != null)
            .GroupBy(m => m.SundayClassId!.Value)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count);
    }

    public async Task CreateClassAsync(string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.SundayClasses.Add(new SundayClass
        {
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<Member>> GetAllMembersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Members.ToListAsync();
    }

    // A member belongs to at most one class, so assigning overwrites any previous class;
    // pass null to remove the member from their class.
    public async Task AssignMemberAsync(int memberId, int? sundayClassId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var member = await db.Members.FindAsync(memberId);
        if (member is null)
        {
            return;
        }

        member.SundayClassId = sundayClassId;
        await db.SaveChangesAsync();
    }
}
