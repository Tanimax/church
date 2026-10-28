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

    [Fact]
    public void MaskEmailsIn_MasksEveryAddressInAFreeText()
    {
        var body = "{\"message\":\"You can only send testing emails to your own email address (owner.name@gmail.com).\"}";

        var masked = LogRedaction.MaskEmailsIn(body);

        Assert.Contains("o***@gmail.com", masked);
        Assert.DoesNotContain("owner.name", masked);
        Assert.StartsWith("{\"message\":\"You can only send", masked);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("Internal Server Error", "Internal Server Error")]
    public void MaskEmailsIn_LeavesTextWithoutAddressAlone(string? text, string expected)
    {
        Assert.Equal(expected, LogRedaction.MaskEmailsIn(text));
    }
}
