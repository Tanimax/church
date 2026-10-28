using ChurchAttendance.SharedKernel.Logging;

namespace ChurchAttendance.Tests.Logging;

public class LogRedactionTests
{
    [Theory]
    [InlineData("marie.dupont@gmail.com", "m***@gmail.com")]
    [InlineData("a@b.ca", "a***@b.ca")]
    [InlineData("pas-un-courriel", "***")]
    [InlineData("@domaine.ca", "***")]
    [InlineData("", "(vide)")]
    [InlineData(null, "(vide)")]
    public void MaskEmail_KeepsOnlyTheFirstLetterAndTheDomain(string? email, string expected)
    {
        Assert.Equal(expected, LogRedaction.MaskEmail(email));
    }
}
