using ChurchAttendance.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace ChurchAttendance.Tests.Logging;

public class EmailServiceLoggingTests
{
    [Fact]
    public async Task MissingApiKey_LogsAWarning_WithoutTheFullAddress()
    {
        var logger = new FakeLogger<EmailService>();
        var service = new EmailService(new HttpClient(), new ConfigurationBuilder().Build(), logger);

        await service.SendWelcomeEmailAsync("marie.dupont@gmail.com", "Marie Dupont", "https://eglise.example/card/abc");

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("m***@gmail.com", record.Message);
        Assert.DoesNotContain("marie.dupont", record.Message);
        Assert.DoesNotContain("Marie", record.Message);
    }
}
