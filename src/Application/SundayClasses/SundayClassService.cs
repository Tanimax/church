using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Domain;
using ChurchAttendance.SharedKernel;
using ChurchAttendance.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.SundayClasses;

// Uses IAppDbContextFactory (not a scoped context) because its callers are Blazor Server
// components on long-lived circuits — each operation gets its own short-lived context.
public class SundayClassService(IAppDbContextFactory dbFactory)
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

    // Refused when a class already has the same name, ignoring case and accents
    // ("Adolescents" / "adolescents"): two such classes can't be told apart in the lists.
    public async Task<Result> CreateClassAsync(string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existingNames = await db.SundayClasses.Select(c => c.Name).ToListAsync();
        if (existingNames.Any(existing => TextSearch.SameText(existing, name)))
        {
            return SundayClassErrors.NameTaken;
        }

        db.SundayClasses.Add(new SundayClass
        {
            Name = name.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return Result.Success();
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
