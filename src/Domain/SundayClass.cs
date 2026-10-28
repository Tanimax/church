namespace ChurchAttendance.Domain;

public class SundayClass
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
}
