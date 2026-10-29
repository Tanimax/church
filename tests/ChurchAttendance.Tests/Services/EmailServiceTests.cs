using System.Net;
using System.Text.Json;
using ChurchAttendance.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace ChurchAttendance.Tests.Services;

// The welcome email goes out through Resend's HTTP API; a fake handler stands in for it so the
// request can be checked, and so its failures can be played out.
public class EmailServiceTests
{
    private const string CardUrl = "https://eglise.example/card/abc";

    private static EmailService Create(HttpMessageHandler handler, FakeLogger<EmailService> logger, string? fromAddress = null)
    {
        var settings = new Dictionary<string, string?> { ["ResendApiKey"] = "re_test_key" };
        if (fromAddress is not null)
        {
            settings["ResendFromAddress"] = fromAddress;
        }
        return new EmailService(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), logger);
    }

    [Fact]
    public async Task SendsTheCardLink_ToTheMember_ThroughResend()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var logger = new FakeLogger<EmailService>();

        await Create(handler, logger, "accueil@eglise.example").SendWelcomeEmailAsync("marie@exemple.ca", "Marie <Dupont>", CardUrl);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.resend.com/emails", request.Uri);
        Assert.Equal("Bearer re_test_key", request.Authorization);
        using var payload = JsonDocument.Parse(request.Body);
        Assert.Equal("accueil@eglise.example", payload.RootElement.GetProperty("from").GetString());
        Assert.Equal(["marie@exemple.ca"], payload.RootElement.GetProperty("to").EnumerateArray().Select(e => e.GetString()));
        var html = payload.RootElement.GetProperty("html").GetString()!;
        Assert.Contains($"href=\"{CardUrl}\"", html);
        Assert.Contains("Bonjour Marie &lt;Dupont&gt;,", html); // the name is HTML-encoded
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task WithoutAFromAddress_UsesResendsDefaultSender()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Create(handler, new FakeLogger<EmailService>()).SendWelcomeEmailAsync("marie@exemple.ca", "Marie", CardUrl);

        using var payload = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        Assert.Equal("onboarding@resend.dev", payload.RootElement.GetProperty("from").GetString());
    }

    [Fact]
    public async Task RefusedByResend_LogsAWarning_WithTheAddressesMasked()
    {
        var handler = new RecordingHandler(HttpStatusCode.UnprocessableEntity, """{"message":"Invalid `to`: marie.dupont@gmail.com"}""");
        var logger = new FakeLogger<EmailService>();

        await Create(handler, logger).SendWelcomeEmailAsync("marie.dupont@gmail.com", "Marie", CardUrl);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("UnprocessableEntity", record.Message);
        Assert.Contains("m***@gmail.com", record.Message);
        Assert.DoesNotContain("marie.dupont", record.Message);
    }

    [Fact]
    public async Task ResendUnreachable_LogsAWarning_InsteadOfFailingTheCaller()
    {
        // A failed email must never block the member's creation.
        var logger = new FakeLogger<EmailService>();

        await Create(new ThrowingHandler(), logger).SendWelcomeEmailAsync("marie@exemple.ca", "Marie", CardUrl);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.IsType<HttpRequestException>(record.Exception);
        Assert.Contains("m***@exemple.ca", record.Message);
    }

    private sealed record SentRequest(HttpMethod Method, string Uri, string? Authorization, string Body);

    private sealed class RecordingHandler(HttpStatusCode status, string responseBody = "{}") : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new SentRequest(request.Method, request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(), await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connexion refusée");
    }
}
