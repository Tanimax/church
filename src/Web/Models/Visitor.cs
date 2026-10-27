namespace ChurchAttendance.Models;

public class Visitor
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly VisitDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
