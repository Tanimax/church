namespace ChurchAttendance.Models;

public class Attendance
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public Member? Member { get; set; }
    public int ServiceSessionId { get; set; }
    public ServiceSession? ServiceSession { get; set; }
    public DateTime CheckedInAt { get; set; }
    public string? CheckedInBy { get; set; }
    public AttendanceType Type { get; set; } = AttendanceType.Culte;
}
