using ChurchAttendance.Common;
using Microsoft.AspNetCore.Http;

namespace ChurchAttendance.Tests.Common;

public class PublicUrlsTests
{
    [Theory]
    [InlineData("https://eglise.example/")]
    [InlineData("https://eglise.example")]
    public void Card_JoinsBaseUrlAndTokenWithSingleSlash(string baseUrl)
    {
        Assert.Equal("https://eglise.example/card/abc", PublicUrls.Card(baseUrl, "abc"));
    }

    [Fact]
    public void Card_FromRequest_UsesRequestSchemeAndHost()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("eglise.example");

        Assert.Equal("https://eglise.example/card/abc", PublicUrls.Card(context.Request, "abc"));
        Assert.Equal("https://eglise.example/visitor", PublicUrls.VisitorForm(context.Request));
    }

    [Fact]
    public void VisitorForm_TrimsTrailingSlash()
    {
        Assert.Equal("https://eglise.example/visitor", PublicUrls.VisitorForm("https://eglise.example/"));
    }
}
