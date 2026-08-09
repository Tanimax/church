using System.Text.Json.Serialization;
using ChurchAttendance.Components;
using ChurchAttendance.Data;
using ChurchAttendance.Endpoints;
using ChurchAttendance.Models;
using ChurchAttendance.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

// Only used to seed the very first "admin" account below — after that, credentials
// live in the Users table and this variable is no longer consulted.
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

var connectionString = BuildPostgresConnectionString(builder.Configuration);

// IDbContextFactory is used by Blazor Server components, which run on a long-lived circuit
// and must create a short-lived context per operation instead of sharing one scoped instance.
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));

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

    // One-time bootstrap: the app used to gate the whole admin area behind a single
    // shared ADMIN_PASSWORD. The first time this runs against a database with no user
    // accounts yet, create an "admin" account with that same password so existing
    // deployments keep working without any manual step.
    if (!await db.Users.AnyAsync())
    {
        db.Users.Add(new User
        {
            Username = UserService.NormalizeUsername("admin"),
            PasswordHash = UserService.HashPassword(adminPassword),
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
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

// The scanner is served as static files, so [Authorize] never applies to it — gate it
// here instead, before static file serving, since anyone with the URL could otherwise
// use it to record attendance without being an admin.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/scanner") && context.User.Identity?.IsAuthenticated != true)
    {
        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/admin/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }
    await next();
});

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

// Fly Postgres (managed or unmanaged) exposes DATABASE_URL as a libpq-style URI
// (postgres://user:pass@host:port/db), not the ADO.NET keyword=value format Npgsql
// expects — parse it when there's no explicit ConnectionStrings:Default override.
static string BuildPostgresConnectionString(IConfiguration configuration)
{
    var explicitConnectionString = configuration.GetConnectionString("Default");
    if (!string.IsNullOrEmpty(explicitConnectionString))
    {
        return explicitConnectionString;
    }

    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        // Fly's internal Postgres (accessed over the private flycast network) doesn't
        // offer SSL and aborts the connection outright when a client asks to negotiate
        // it — respect the sslmode the platform put in DATABASE_URL instead of assuming.
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        var sslMode = query.TryGetValue("sslmode", out var sslModeValue)
            && Enum.TryParse<SslMode>(sslModeValue, ignoreCase: true, out var parsedSslMode)
                ? parsedSslMode
                : SslMode.Prefer;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = sslMode
        };
        return builder.ConnectionString;
    }

    return "Host=localhost;Port=5432;Database=churchattendance;Username=postgres;Password=postgres";
}

// Exposed so the test project can host this app via WebApplicationFactory<Program>.
public partial class Program;
