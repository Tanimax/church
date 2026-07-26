using System.Text.Json.Serialization;
using ChurchAttendance.Components;
using ChurchAttendance.Data;
using ChurchAttendance.Endpoints;
using ChurchAttendance.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Serialize enums (e.g. AttendanceType) as their string name in JSON API payloads,
// so the scanner's JS client can send/read readable values like "SainteCene" instead of "1".
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// By default, ASP.NET Core generates a fresh Data Protection key on every process start
// and keeps it only in the container's ephemeral filesystem. Since this app scales to zero
// and restarts on each new request, that would invalidate every antiforgery/circuit token
// (and the admin auth cookie) on every cold start. Persisting keys to the mounted volume
// keeps them stable across restarts.
var dataProtectionKeysPath = builder.Configuration["DataProtectionKeysPath"];
if (!string.IsNullOrEmpty(dataProtectionKeysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/login";
        options.Cookie.Name = "ChurchAttendance.Admin";
    });
builder.Services.AddAuthorization();

var adminPassword = builder.Configuration["AdminPassword"]
    ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

if (string.IsNullOrEmpty(adminPassword))
{
    if (builder.Environment.IsDevelopment())
    {
        adminPassword = "dev-only-password";
        Console.WriteLine("ATTENTION: ADMIN_PASSWORD non défini — mot de passe de développement 'dev-only-password' utilisé.");
    }
    else
    {
        throw new InvalidOperationException("La variable d'environnement ADMIN_PASSWORD doit être définie en production.");
    }
}

builder.Configuration["AdminPassword"] = adminPassword;

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=churchattendance.db";

// IDbContextFactory is used by Blazor Server components, which run on a long-lived circuit
// and must create a short-lived context per operation instead of sharing one scoped instance.
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));

// Minimal API endpoints inject AppDbContext directly (scoped, one instance per HTTP request),
// derived from the same factory so there's a single DbContextOptions registration.
builder.Services.AddScoped<AppDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

builder.Services.AddHttpClient<EmailService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Fly.io terminates TLS at its edge proxy and forwards plain HTTP to the container,
// so the app must trust X-Forwarded-Proto to know the original request was HTTPS
// (otherwise generated links like the member card URL come out as http://).
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Configure the HTTP request pipeline.
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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapMembersEndpoints();
app.MapCardEndpoints();
app.MapCheckInEndpoints();
app.MapReportsEndpoints();
app.MapAuthEndpoints();
app.MapVisitorEndpoints();

app.MapGet("/scanner", () => Results.Redirect("/scanner/index.html"));

app.Run();

// Exposed so the test project can host this app via WebApplicationFactory<Program>.
public partial class Program;
