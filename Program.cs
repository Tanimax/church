using System.Text.Json.Serialization;
using ChurchAttendance.Common;
using ChurchAttendance.Components;
using ChurchAttendance.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Serialize enums (e.g. AttendanceType) as their string name in JSON API payloads,
// so the scanner's JS client can send/read readable values like "SainteCene" instead of "1".
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddPersistentDataProtection(builder.Configuration);
builder.Services.AddAppAuthentication();

// Resolved before Build() so a production deploy without ADMIN_PASSWORD fails at startup.
var adminPassword = DatabaseSetup.ResolveSeedAdminPassword(builder.Configuration, builder.Environment);
builder.Services.AddAppDatabase(builder.Configuration);

builder.Services.AddChurchClock(builder.Configuration);
builder.Services.AddAppFeatures();

var app = builder.Build();

await app.MigrateAndSeedAsync(adminPassword);

app.UseFlyForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.UseScannerAuthGate();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAppEndpoints();

app.Run();

// Exposed so the test project can host this app via WebApplicationFactory<Program>.
public partial class Program;
