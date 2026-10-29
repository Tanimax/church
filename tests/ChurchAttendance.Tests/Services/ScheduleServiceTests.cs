using ChurchAttendance.Application.Abstractions;
using ChurchAttendance.Application.Schedules;
using ChurchAttendance.Infrastructure.Persistence;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ChurchAttendance.Tests.Services;

public class ScheduleServiceTests
{
    private static ScheduleService CreateService(CustomWebApplicationFactory factory) =>
        new(factory.Services.GetRequiredService<IAppDbContextFactory>(), factory.Services.GetRequiredService<ChurchClock>());

    [Fact]
    public async Task GetAsync_UnsavedQuarter_ListsItsSundaysEmpty_WithTheDefaultNote()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);

        var form = await service.GetAsync(2026, 4);

        Assert.False(form.Exists);
        Assert.Equal(ServiceSchedule.SundaysOf(2026, 4), form.Rows.Select(r => r.Date));
        Assert.All(form.Rows, r => { Assert.Null(r.Leader); Assert.Null(r.Preacher); });
        Assert.Equal(13, form.MissingCount);
        Assert.Equal(ScheduleService.DefaultFooterNote, form.FooterNote);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_KeepsTrimmedNames_AndBlankAsEmpty()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        var form = await service.GetAsync(2026, 4);
        form.Rows[0].Leader = "  Sr Margarette Gilcin ";
        form.Rows[0].Preacher = "Pasteur Onis Clermont";
        form.Rows[1].Leader = "   ";
        form.FooterNote = "N.B. Appeler la sœur Nativita.";

        var result = await service.SaveAsync(form);
        var reloaded = await service.GetAsync(2026, 4);

        Assert.True(result.IsSuccess);
        Assert.True(reloaded.Exists);
        Assert.Equal("Sr Margarette Gilcin", reloaded.Rows[0].Leader);
        Assert.Equal("Pasteur Onis Clermont", reloaded.Rows[0].Preacher);
        Assert.Null(reloaded.Rows[1].Leader);
        Assert.Equal(12, reloaded.MissingCount);
        Assert.Equal("N.B. Appeler la sœur Nativita.", reloaded.FooterNote);
    }

    [Fact]
    public async Task SaveAsync_Twice_UpdatesTheSameQuarter()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        var form = await service.GetAsync(2026, 4);
        form.Rows[0].Preacher = "Pasteur Jacob Milien";
        await service.SaveAsync(form);

        var second = await service.GetAsync(2026, 4);
        second.Rows[0].Preacher = "Rév. Guillaume Cangas";
        await service.SaveAsync(second);

        await using var db = factory.CreateDbContext();
        Assert.Single(db.ServiceSchedules);
        Assert.Equal(13, db.ScheduledServices.Count());
        Assert.Equal("Rév. Guillaume Cangas", (await service.GetAsync(2026, 4)).Rows[0].Preacher);
    }

    [Fact]
    public async Task GetAsync_NewQuarter_StartsFromThePreviousQuartersNote()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        var q4 = await service.GetAsync(2026, 4);
        q4.FooterNote = "Si la date ne vous convient pas, communiquer avec la sœur Nativita.";
        await service.SaveAsync(q4);

        var q1 = await service.GetAsync(2027, 1);

        Assert.False(q1.Exists);
        Assert.Equal("Si la date ne vous convient pas, communiquer avec la sœur Nativita.", q1.FooterNote);
    }

    [Fact]
    public async Task SaveAsync_DatesNotThoseOfTheQuarter_IsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        var form = await service.GetAsync(2026, 4);
        form.Rows[0].Date = new DateOnly(2026, 10, 5); // a Monday

        var result = await service.SaveAsync(form);

        Assert.Equal(ScheduleErrors.WrongSundays, result.Error);
        await using var db = factory.CreateDbContext();
        Assert.Empty(db.ServiceSchedules);
    }

    [Fact]
    public async Task SaveAsync_NameTooLong_IsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        var form = await service.GetAsync(2026, 4);
        form.Rows[0].Leader = new string('a', ScheduleService.MaxNameLength + 1);

        Assert.Equal(ScheduleErrors.NameTooLong, (await service.SaveAsync(form)).Error);
    }

    [Fact]
    public async Task SaveAsync_QuarterOpenedEmptyThenSavedBySomeoneElse_LastSaveWins()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        var mine = await service.GetAsync(2026, 4);     // both open the empty quarter
        var theirs = await service.GetAsync(2026, 4);
        theirs.Rows[0].Leader = "Sr Mona Y. Cangas";
        await service.SaveAsync(theirs);

        mine.Rows[0].Leader = "Fr Stanley Seraphin";
        var result = await service.SaveAsync(mine);

        // By the time "mine" is saved the quarter exists, so it is updated: one quarter, no error.
        Assert.True(result.IsSuccess);
        await using var db = factory.CreateDbContext();
        Assert.Single(db.ServiceSchedules);
        Assert.Equal("Fr Stanley Seraphin", (await service.GetAsync(2026, 4)).Rows[0].Leader);
    }

    [Fact]
    public async Task SaveAsync_QuarterCreatedBetweenCheckAndInsert_ReturnsSavedMeanwhile()
    {
        using var factory = new CustomWebApplicationFactory();
        var form = await CreateService(factory).GetAsync(2026, 4);
        form.Rows[0].Leader = "Fr Stanley Seraphin";
        var service = new ScheduleService(
            new InterceptedFactory(factory.ConnectionString, new SomeoneElseCreatesTheQuarter(factory.ConnectionString)),
            factory.Services.GetRequiredService<ChurchClock>());

        var result = await service.SaveAsync(form);

        Assert.Equal(ScheduleErrors.SavedMeanwhile, result.Error);
        await using var db = factory.CreateDbContext();
        Assert.Single(db.ServiceSchedules);
        Assert.Empty(db.ScheduledServices); // the other person's (empty) version is kept as is
    }

    // Another secretary saves Q4 2026 in the window between the service's "does it exist?"
    // query and its own insert, so the unique (Year, Quarter) index rejects the service's row.
    private sealed class SomeoneElseCreatesTheQuarter(string connectionString) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await using var other = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);
                other.ServiceSchedules.Add(new ServiceSchedule { Year = 2026, Quarter = 4, UpdatedAt = DateTime.UtcNow });
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
    public async Task GetNameSuggestionsAsync_EveryNameOnce_Sorted()
    {
        using var factory = new CustomWebApplicationFactory();
        var service = CreateService(factory);
        var form = await service.GetAsync(2026, 4);
        form.Rows[0].Leader = "Sr Mona Y. Cangas";
        form.Rows[0].Preacher = "Pasteur Onis Clermont";
        form.Rows[1].Leader = "La jeunesse";
        form.Rows[1].Preacher = "Sr Mona Y. Cangas";   // leader one week, preacher another
        await service.SaveAsync(form);

        var names = await service.GetNameSuggestionsAsync();

        Assert.Equal(["La jeunesse", "Pasteur Onis Clermont", "Sr Mona Y. Cangas"], names);
    }
}
