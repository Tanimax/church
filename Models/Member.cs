namespace ChurchAttendance.Models;

public class Member
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PhotoUrl { get; set; }
    public required string Token { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Gender? Gender { get; set; }
    public bool IsBaptized { get; set; }
}
