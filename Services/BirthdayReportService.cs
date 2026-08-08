using ChurchAttendance.Models;

namespace ChurchAttendance.Services;

public static class BirthdayReportService
{
    // Matches members whose birthday (day + month, year-agnostic) falls on one of the
    // 7 calendar days starting at weekStart — handles the week crossing a month/year
    // boundary since matching is done against real dates, not raw day/month numbers.
    public static List<(DateOnly Date, Member Member)> FindBirthdaysInWeek(
        IEnumerable<Member> members, DateOnly weekStart)
    {
        var memberList = members.ToList();
        var weekDates = Enumerable.Range(0, 7).Select(weekStart.AddDays);

        return weekDates
            .SelectMany(date => memberList
                .Where(m => m.BirthMonth == date.Month && m.BirthDay == date.Day)
                .Select(m => (Date: date, Member: m)))
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Member.LastName)
            .ThenBy(x => x.Member.FirstName)
            .ToList();
    }
}
