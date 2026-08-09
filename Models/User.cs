namespace ChurchAttendance.Models;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.Secretaire;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
