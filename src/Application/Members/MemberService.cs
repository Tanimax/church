using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Errors;
using ChurchAttendance.Domain;
using ChurchAttendance.SharedKernel;
using ChurchAttendance.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.Members;

// Member management, shared by the Membres page and the /api/members endpoint so there is a
// single way to create a member. Uses IAppDbContextFactory because some callers are Blazor
// Server components on long-lived circuits.
public class MemberService(IAppDbContextFactory dbFactory, IEmailSender emailSender, ChurchClock clock)
{
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
            CreatedAt = clock.UtcNow
        };
        ApplyDetails(member, form);
        db.Members.Add(member);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync();
                break;
            }
            catch (ConflictException) when (attempt < 3)
            {
                // The random card token collided with an existing one (unique index):
                // vanishingly rare, so just draw another.
                member.Token = TokenService.GenerateToken();
            }
        }

        if (!string.IsNullOrWhiteSpace(member.Email))
        {
            await emailSender.SendWelcomeEmailAsync(member.Email, member.FullName, PublicUrls.Card(baseUri, member.Token));
        }

        return member;
    }

    public async Task<Result> UpdateMemberAsync(int memberId, MemberFormModel form)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var member = await db.Members.FindAsync(memberId);
        if (member is null)
        {
            return MemberErrors.NotFound;
        }

        member.FirstName = form.FirstName.Trim();
        member.LastName = form.LastName.Trim();
        ApplyDetails(member, form);
        await db.SaveChangesAsync();
        return Result.Success();
    }

    // On success, the member's new IsActive value.
    public async Task<Result<bool>> ToggleActiveAsync(int memberId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var member = await db.Members.FindAsync(memberId);
        if (member is null)
        {
            return MemberErrors.NotFound;
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
