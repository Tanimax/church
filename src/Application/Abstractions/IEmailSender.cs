namespace ChurchAttendance.Application.Abstractions;

public interface IEmailSender
{
    Task SendWelcomeEmailAsync(string toEmail, string fullName, string cardUrl);
}
