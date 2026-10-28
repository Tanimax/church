namespace ChurchAttendance.Tests.Common;

public class TextSearchTests
{
    [Theory]
    [InlineData("Éloïse Gagné", "eloise")]
    [InlineData("Éloïse Gagné", "GAGNE")]
    [InlineData("Éloïse Gagné", "loïse g")]
    [InlineData("François Côté", "francois cote")]
    [InlineData("Jean Dupont", " jean ")]
    [InlineData("Jean Dupont", "")]
    [InlineData("Jean Dupont", null)]
    public void Contains_IgnoresCaseAccentsAndSurroundingSpaces(string text, string? term)
    {
        Assert.True(TextSearch.Contains(text, term));
    }

    [Theory]
    [InlineData("Jean Dupont", "Martin")]
    [InlineData(null, "jean")]
    public void Contains_NoMatch(string? text, string term)
    {
        Assert.False(TextSearch.Contains(text, term));
    }

    [Theory]
    [InlineData("Adolescents", " adolescents ", true)]
    [InlineData("Préados", "PREADOS", true)]
    [InlineData("Adultes", "Adolescents", false)]
    public void SameText_IgnoresCaseAccentsAndSurroundingSpaces(string a, string b, bool expected)
    {
        Assert.Equal(expected, TextSearch.SameText(a, b));
    }
}
