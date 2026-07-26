using System.Security.Cryptography;

namespace ChurchAttendance.Services;

public static class TokenService
{
    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
