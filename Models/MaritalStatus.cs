namespace ChurchAttendance.Models;

public enum MaritalStatus
{
    Celibataire = 0,
    Marie = 1,
    Divorce = 2,
    Veuf = 3
}

public static class MaritalStatusExtensions
{
    public static string ToLabel(this MaritalStatus status) => status switch
    {
        MaritalStatus.Celibataire => "Célibataire",
        MaritalStatus.Marie => "Marié(e)",
        MaritalStatus.Divorce => "Divorcé(e)",
        MaritalStatus.Veuf => "Veuf(ve)",
        _ => status.ToString()
    };
}
