using System.Text.Json.Serialization;
using ChurchAttendance.Application;
using ChurchAttendance.Components;
using ChurchAttendance.Infrastructure;
using ChurchAttendance.Infrastructure.Persistence;
using ChurchAttendance.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Serialize enums (e.g. AttendanceType) as their string name in JSON API payloads,
// so the scanner's JS client can send/read readable values like "SainteCene" instead of "1".
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddPersistentDataProtection(builder.Configuration);
builder.Services.AddAppAuthentication();
builder.Services.AddAppErrorHandling();
builder.Services.AddHealthChecks();

// Resolved before Build() so a production deploy without ADMIN_PASSWORD fails at startup.
var adminPassword = DatabaseSetup.ResolveSeedAdminPassword(builder.Configuration, builder.Environment);

builder.Services.AddChurchClock(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

await app.MigrateAndSeedAsync(adminPassword);

app.UseFlyForwardedHeaders();

app.UseAppErrorHandling();
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.UseScannerAuthGate();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAppEndpoints();

// Liveness for Fly's health check (fly.toml): answers as soon as the app serves requests.
// It deliberately doesn't query the database: a database outage is already reported to users
// (503 / error page), and failing this check would make Fly stop routing to the only machine.
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Exposed so the test project can host this app via WebApplicationFactory<Program>.
public partial class Program;
