using ChurchAttendance.Data;
using ChurchAttendance.Models;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Features.Members;

// Member management for the Membres page. Uses IDbContextFactory because its callers
// are Blazor Server components on long-lived circuits.
public class MemberService(IDbContextFactory<AppDbContext> dbFactory, EmailService emailService)
{
    public static string CardUrl(string baseUri, string token) => $"{baseUri.TrimEnd('/')}/card/{token}";

    public async Task<List<Member>> GetMembersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Members.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToListAsync();
    }

    // Creates the member with a fresh card token and, when they have an email address,
    // sends them a welcome email linking to their card.
    public async Task<Member> CreateMemberAsync(MemberFormModel form, string baseUri)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var member = new Member
        {
            FirstName = form.FirstName.Trim(),
            LastName = form.LastName.Trim(),
            Token = TokenService.GenerateToken(),
            CreatedAt = DateTime.UtcNow
        };
        ApplyDetails(member, form);
        db.Members.Add(member);
        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(member.Email))
        {
            await emailService.SendWelcomeEmailAsync(member.Email, member.FullName, CardUrl(baseUri, member.Token));
        }

        return member;
    }

    // Returns false when the member no longer exists.
    public async Task<bool> UpdateMemberAsync(int memberId, MemberFormModel form)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var member = await db.Members.FindAsync(memberId);
        if (member is null)
        {
            return false;
        }

        member.FirstName = form.FirstName.Trim();
        member.LastName = form.LastName.Trim();
        ApplyDetails(member, form);
        await db.SaveChangesAsync();
        return true;
    }

    // Returns the new IsActive value, or null when the member no longer exists.
    public async Task<bool?> ToggleActiveAsync(int memberId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var member = await db.Members.FindAsync(memberId);
        if (member is null)
        {
            return null;
        }

        member.IsActive = !member.IsActive;
        await db.SaveChangesAsync();
        return member.IsActive;
    }

    private static void ApplyDetails(Member member, MemberFormModel form)
    {
        member.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
        member.Email = string.IsNullOrWhiteSpace(form.Email) ? null : form.Email.Trim();
        member.IsBaptized = form.IsBaptized;
        member.BirthDay = form.BirthDay;
        member.BirthMonth = form.BirthMonth;
    }
}
