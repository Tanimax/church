using ChurchAttendance.Services;

namespace ChurchAttendance.Tests.Services;

public class TokenServiceTests
{
    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var token = TokenService.GenerateToken();

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateToken_ProducesUrlSafeCharactersOnly()
    {
        var token = TokenService.GenerateToken();

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void GenerateToken_HasExpectedLengthFor16Bytes()
    {
        var token = TokenService.GenerateToken();

        // 16 random bytes base64-encoded without padding = 22 characters.
        Assert.Equal(22, token.Length);
    }

    [Fact]
    public void GenerateToken_ConsecutiveCallsProduceDistinctValues()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => TokenService.GenerateToken()).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }
}
