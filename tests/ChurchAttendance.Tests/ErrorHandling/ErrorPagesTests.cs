using ChurchAttendance.Tests.Infrastructure;

namespace ChurchAttendance.Tests.ErrorHandling;

public class ErrorPagesTests
{
    [Fact]
    public async Task ErrorPage_IsInFrench_AndShowsNoTechnicalAdvice()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var html = await client.GetStringAsync("/Error");

        Assert.Contains("Une erreur est survenue", html);
        Assert.DoesNotContain("Development", html);
    }

    [Fact]
    public async Task UnknownPage_ShowsTheFrenchNotFoundPage()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/n-existe-pas");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Page introuvable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownCardLink_ShowsAnHtmlPage_NotAJsonString()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/card/n-existe-pas");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Carte introuvable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HealthCheck_AnswersWithoutLogin()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
