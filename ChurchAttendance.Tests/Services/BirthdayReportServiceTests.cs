using ChurchAttendance.Models;
using ChurchAttendance.Services;

namespace ChurchAttendance.Tests.Services;

public class BirthdayReportServiceTests
{
    private static Member MakeMember(string first, string last, int? birthDay, int? birthMonth) => new()
    {
        FirstName = first,
        LastName = last,
        Token = Guid.NewGuid().ToString(),
        BirthDay = birthDay,
        BirthMonth = birthMonth
    };

    [Fact]
    public void FindBirthdaysInWeek_MatchesSundayBoundary()
    {
        var weekStart = new DateOnly(2026, 2, 1); // Sunday
        var member = MakeMember("A", "Sun", 1, 2);

        var results = BirthdayReportService.FindBirthdaysInWeek([member], weekStart);

        Assert.Single(results);
        Assert.Equal(new DateOnly(2026, 2, 1), results[0].Date);
    }

    [Fact]
    public void FindBirthdaysInWeek_MatchesSaturdayBoundary()
    {
        var weekStart = new DateOnly(2026, 2, 1);
        var member = MakeMember("C", "Sat", 7, 2);

        var results = BirthdayReportService.FindBirthdaysInWeek([member], weekStart);

        Assert.Single(results);
        Assert.Equal(new DateOnly(2026, 2, 7), results[0].Date);
    }

    [Fact]
    public void FindBirthdaysInWeek_ExcludesDayBeforeWeek()
    {
        var weekStart = new DateOnly(2026, 2, 1);
        var member = MakeMember("D", "Before", 31, 1);

        var results = BirthdayReportService.FindBirthdaysInWeek([member], weekStart);

        Assert.Empty(results);
    }

    [Fact]
    public void FindBirthdaysInWeek_ExcludesDayAfterWeek()
    {
        var weekStart = new DateOnly(2026, 2, 1);
        var member = MakeMember("E", "After", 8, 2);

        var results = BirthdayReportService.FindBirthdaysInWeek([member], weekStart);

        Assert.Empty(results);
    }

    [Fact]
    public void FindBirthdaysInWeek_ExcludesMembersWithoutBirthday()
    {
        var weekStart = new DateOnly(2026, 2, 1);
        var member = MakeMember("G", "NoBirthday", null, null);

        var results = BirthdayReportService.FindBirthdaysInWeek([member], weekStart);

        Assert.Empty(results);
    }

    [Fact]
    public void FindBirthdaysInWeek_HandlesMonthBoundaryCorrectly()
    {
        // Week spans Jan 29 - Feb 4; a Jan 31 birthday and a Feb 2 birthday should both match,
        // while a Jan 28 (before) and Feb 5 (after) birthday should not.
        var weekStart = new DateOnly(2026, 1, 29);
        var members = new[]
        {
            MakeMember("InJan", "X", 31, 1),
            MakeMember("InFeb", "Y", 2, 2),
            MakeMember("BeforeWeek", "Z", 28, 1),
            MakeMember("AfterWeek", "W", 5, 2)
        };

        var results = BirthdayReportService.FindBirthdaysInWeek(members, weekStart);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Member.FirstName == "InJan" && r.Date == new DateOnly(2026, 1, 31));
        Assert.Contains(results, r => r.Member.FirstName == "InFeb" && r.Date == new DateOnly(2026, 2, 2));
    }

    [Fact]
    public void FindBirthdaysInWeek_OrdersChronologicallyThenByName()
    {
        var weekStart = new DateOnly(2026, 2, 1);
        var members = new[]
        {
            MakeMember("Zed", "Later", 7, 2),
            MakeMember("Bea", "Zeta", 1, 2),
            MakeMember("Amy", "Alpha", 1, 2)
        };

        var results = BirthdayReportService.FindBirthdaysInWeek(members, weekStart);

        Assert.Equal(["Alpha", "Zeta", "Later"], results.Select(r => r.Member.LastName));
    }

    [Fact]
    public void FindBirthdaysInWeek_NoMatches_ReturnsEmptyList()
    {
        var weekStart = new DateOnly(2026, 2, 1);
        var member = MakeMember("H", "NoMatch", 15, 6);

        var results = BirthdayReportService.FindBirthdaysInWeek([member], weekStart);

        Assert.Empty(results);
    }
}
