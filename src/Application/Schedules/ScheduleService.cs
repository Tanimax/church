using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Errors;
using ChurchAttendance.Domain;
using ChurchAttendance.SharedKernel;
using ChurchAttendance.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Application.Schedules;

// The quarterly Sunday service schedule (Programmation page). Uses IAppDbContextFactory
// because its caller is a Blazor Server component on a long-lived circuit.
public class ScheduleService(IAppDbContextFactory dbFactory, ChurchClock clock)
{
    public const int MaxNameLength = 100;
    public const int MaxFooterLength = 300;

    // Used for a church's very first quarter; later quarters start from the previous one's note.
    public const string DefaultFooterNote = "Si la date ne vous convient pas, communiquer avec le secrétariat.";

    public (int Year, int Quarter) CurrentQuarter => (clock.Today.Year, ServiceSchedule.QuarterOf(clock.Today));

    // The quarter's Sundays with what is saved for them; a quarter never saved comes back
    // empty, with the most recent quarter's footer note.
    public async Task<ScheduleFormModel> GetAsync(int year, int quarter)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var schedule = await db.ServiceSchedules
            .Include(s => s.Services)
            .SingleOrDefaultAsync(s => s.Year == year && s.Quarter == quarter);

        var footer = schedule?.FooterNote;
        if (schedule is null)
        {
            footer = await db.ServiceSchedules
                .Where(s => s.Year < year || (s.Year == year && s.Quarter < quarter))
                .OrderByDescending(s => s.Year).ThenByDescending(s => s.Quarter)
                .Select(s => s.FooterNote)
                .FirstOrDefaultAsync() ?? DefaultFooterNote;
        }

        return new ScheduleFormModel
        {
            Year = year,
            Quarter = quarter,
            Exists = schedule is not null,
            FooterNote = footer,
            Rows = ServiceSchedule.SundaysOf(year, quarter)
                .Select(date =>
                {
                    var saved = schedule?.Services.FirstOrDefault(s => s.Date == date);
                    return new ScheduleRow { Date = date, Leader = saved?.Leader, Preacher = saved?.Preacher };
                })
                .ToList(),
        };
    }

    // Saves every row (blank names allowed: a quarter is filled in over several sessions).
    public async Task<Result> SaveAsync(ScheduleFormModel form)
    {
        var sundays = ServiceSchedule.SundaysOf(form.Year, form.Quarter);
        if (!form.Rows.Select(r => r.Date).SequenceEqual(sundays))
        {
            return ScheduleErrors.WrongSundays;
        }
        if (form.Rows.Any(r => Clean(r.Leader)?.Length > MaxNameLength || Clean(r.Preacher)?.Length > MaxNameLength))
        {
            return ScheduleErrors.NameTooLong;
        }
        if (Clean(form.FooterNote)?.Length > MaxFooterLength)
        {
            return ScheduleErrors.FooterTooLong;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var schedule = await db.ServiceSchedules
            .Include(s => s.Services)
            .SingleOrDefaultAsync(s => s.Year == form.Year && s.Quarter == form.Quarter);
        var isNew = schedule is null;
        if (schedule is null)
        {
            schedule = new ServiceSchedule { Year = form.Year, Quarter = form.Quarter };
            db.ServiceSchedules.Add(schedule);
        }

        foreach (var row in form.Rows)
        {
            var service = schedule.Services.FirstOrDefault(s => s.Date == row.Date);
            if (service is null)
            {
                service = new ScheduledService { Date = row.Date };
                schedule.Services.Add(service);
            }
            service.Leader = Clean(row.Leader);
            service.Preacher = Clean(row.Preacher);
        }
        schedule.FooterNote = Clean(form.FooterNote);
        schedule.UpdatedAt = clock.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (ConflictException) when (isNew)
        {
            // Someone else saved this quarter first (unique Year + Quarter): don't overwrite it unseen.
            return ScheduleErrors.SavedMeanwhile;
        }
        return Result.Success();
    }

    // Every name already used as leader or preacher, to suggest while typing.
    public async Task<List<string>> GetNameSuggestionsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var leaders = await db.ScheduledServices.Where(s => s.Leader != null).Select(s => s.Leader!).Distinct().ToListAsync();
        var preachers = await db.ScheduledServices.Where(s => s.Preacher != null).Select(s => s.Preacher!).Distinct().ToListAsync();
        return leaders.Concat(preachers)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, ignoreCase: true))
            .ToList();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
