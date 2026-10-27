namespace ChurchAttendance.Models;

public class ServiceSession
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Label { get; set; }
}
