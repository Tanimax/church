using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Reports;
using ChurchAttendance.Domain;
using ChurchAttendance.SharedKernel;
using ChurchAttendance.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.Announcements;

// The catalogue of announcements and the month's theme (Annonces page). Uses
// IAppDbContextFactory because its caller is a Blazor Server component on a long-lived circuit.
public class AnnouncementService(IAppDbContextFactory dbFactory, ChurchClock clock)
{
    // Short enough to fit a 36-point slide.
    public const int MaxHeadingLength = 80;
    public const int MaxScheduleLength = 120;
    public const int MaxEventTimeLength = 60;
    public const int MaxTitleLength = 200;
    public const int MaxDetailLabelLength = 30;
    public const int MaxDetailTextLength = 300;
    public const int MaxDetails = 8;
    public const int MaxThemeLength = 120;

    public DateOnly UpcomingSunday => clock.UpcomingSunday;

    // In the order they are read: by kind, then the dated ones by date, then oldest first.
    public async Task<List<Announcement>> GetAllAsync(bool includeArchived)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var announcements = await db.Announcements
            .Where(a => includeArchived || !a.IsArchived)
            .ToListAsync();
        return Ordered(announcements);
    }

    // What belongs in the announcements read that Sunday.
    public async Task<List<Announcement>> GetShownOnAsync(DateOnly sunday)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var candidates = await db.Announcements
            .Where(a => !a.IsArchived && (a.EventDate == null || (a.EndDate ?? a.EventDate) >= sunday))
            .ToListAsync();
        return Ordered(candidates.Where(a => a.IsShownOn(sunday)));
    }

    // The deck projected that Sunday: its announcements, the month's theme, and the
    // birthdays of the Birthdays report for that Sunday (the week ending on it).
    public async Task<AnnouncementDeck> GetDeckAsync(DateOnly sunday)
    {
        var announcements = await GetShownOnAsync(sunday);
        var theme = await GetThemeAsync(sunday.Year, sunday.Month);

        await using var db = await dbFactory.CreateDbContextAsync();
        var withBirthday = await db.Members
            .Where(m => m.IsActive && m.BirthDay != null && m.BirthMonth != null)
            .ToListAsync();
        var birthdays = BirthdayReportService.FindBirthdaysInWeek(withBirthday, sunday.AddDays(-6))
            .Select(b => b.Member.FullName);

        return AnnouncementDeck.Build(sunday, announcements, theme, birthdays);
    }

    public async Task<Result<int>> SaveAsync(AnnouncementFormModel form)
    {
        var validation = Validate(form);
        if (!validation.IsSuccess)
        {
            return validation.Error;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        Announcement? announcement;
        if (form.Id == 0)
        {
            announcement = new Announcement { Title = "", CreatedAt = clock.UtcNow };
            db.Announcements.Add(announcement);
        }
        else
        {
            announcement = await db.Announcements.SingleOrDefaultAsync(a => a.Id == form.Id);
            if (announcement is null)
            {
                return AnnouncementErrors.NotFound;
            }
        }

        // An event's when is its date, a recurring activity's is its schedule: keep only the
        // one the kind uses, so switching kinds leaves nothing stale behind.
        var dated = form.Kind != AnnouncementKind.Recurring;
        announcement.Kind = form.Kind;
        announcement.Heading = Clean(form.Heading);
        announcement.Schedule = form.Kind == AnnouncementKind.Event ? null : Clean(form.Schedule);
        announcement.EventDate = dated ? form.EventDate : null;
        announcement.EndDate = dated ? form.EndDate : null;
        announcement.EventTime = dated && form.EventDate is not null ? Clean(form.EventTime) : null;
        announcement.Title = Clean(form.Title)!;
        announcement.Details = Details(form)
            .Select(d => new AnnouncementDetail { Label = Clean(d.Label), Text = Clean(d.Text)! })
            .ToList();
        announcement.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync();
        return announcement.Id;
    }

    // Archived: kept for next year's copy, but out of the announcements.
    public async Task<Result> SetArchivedAsync(int id, bool archived)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var announcement = await db.Announcements.SingleOrDefaultAsync(a => a.Id == id);
        if (announcement is null)
        {
            return AnnouncementErrors.NotFound;
        }
        announcement.IsArchived = archived;
        announcement.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<string?> GetThemeAsync(int year, int month)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.MonthlyThemes
            .Where(t => t.Year == year && t.Month == month)
            .Select(t => t.Text)
            .SingleOrDefaultAsync();
    }

    // A blank theme removes it: the slides then go without one.
    public async Task<Result> SaveThemeAsync(int year, int month, string? text)
    {
        var theme = Clean(text);
        if (theme?.Length > MaxThemeLength)
        {
            return AnnouncementErrors.ThemeTooLong;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var saved = await db.MonthlyThemes.SingleOrDefaultAsync(t => t.Year == year && t.Month == month);
        if (theme is null)
        {
            if (saved is not null)
            {
                db.MonthlyThemes.Remove(saved);
            }
        }
        else if (saved is null)
        {
            db.MonthlyThemes.Add(new MonthlyTheme { Year = year, Month = month, Text = theme });
        }
        else
        {
            saved.Text = theme;
        }

        // Two people giving the month a theme at once: the unique (Year, Month) index turns the
        // second save into a ConflictException, which the page reports as "modifiée par
        // quelqu'un d'autre. Réessayez."
        await db.SaveChangesAsync();
        return Result.Success();
    }

    private static Result Validate(AnnouncementFormModel form)
    {
        if (Clean(form.Title) is null)
        {
            return AnnouncementErrors.TitleRequired;
        }
        if (form.Kind == AnnouncementKind.Recurring && Clean(form.Schedule) is null)
        {
            return AnnouncementErrors.ScheduleRequired;
        }
        if (form.Kind == AnnouncementKind.Event && form.EventDate is null)
        {
            return AnnouncementErrors.EventDateRequired;
        }
        if (form.Kind != AnnouncementKind.Recurring && form.EndDate is not null)
        {
            if (form.EventDate is null)
            {
                return AnnouncementErrors.EndDateWithoutStart;
            }
            if (form.EndDate < form.EventDate)
            {
                return AnnouncementErrors.EndBeforeStart;
            }
        }

        var details = Details(form);
        if (details.Count > MaxDetails)
        {
            return AnnouncementErrors.TooManyDetails;
        }
        if (Clean(form.Title)!.Length > MaxTitleLength
            || Clean(form.Heading)?.Length > MaxHeadingLength
            || Clean(form.Schedule)?.Length > MaxScheduleLength
            || Clean(form.EventTime)?.Length > MaxEventTimeLength
            || details.Any(d => Clean(d.Label)?.Length > MaxDetailLabelLength || Clean(d.Text)!.Length > MaxDetailTextLength))
        {
            return AnnouncementErrors.TooLong;
        }
        return Result.Success();
    }

    // The detail lines that say something: a label alone is dropped.
    private static List<AnnouncementDetailRow> Details(AnnouncementFormModel form) =>
        form.Details.Where(d => Clean(d.Text) is not null).ToList();

    private static List<Announcement> Ordered(IEnumerable<Announcement> announcements) =>
        announcements
            .OrderBy(a => a.Kind)
            .ThenBy(a => a.EventDate ?? DateOnly.MaxValue)
            .ThenBy(a => a.Id)
            .ToList();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
