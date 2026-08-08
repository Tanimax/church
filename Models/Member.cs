using System.ComponentModel.DataAnnotations.Schema;

namespace ChurchAttendance.Models;

public class Member
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public required string Token { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public bool IsBaptized { get; set; }

    // Birthday is tracked as day + month only (no year) so the app can
    // surface upcoming birthdays without storing members' ages.
    public int? BirthDay { get; set; }
    public int? BirthMonth { get; set; }

    private static readonly string[] MonthNames =
    [
        "", "janvier", "février", "mars", "avril", "mai", "juin",
        "juillet", "août", "septembre", "octobre", "novembre", "décembre"
    ];

    [NotMapped]
    public string? BirthdayLabel => BirthDay.HasValue && BirthMonth.HasValue
        ? $"{BirthDay} {MonthNames[BirthMonth.Value]}"
        : null;
}
