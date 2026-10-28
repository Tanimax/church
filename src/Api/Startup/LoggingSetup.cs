using System.Collections;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging.Console;

namespace ChurchAttendance.Startup;

public static class LoggingSetup
{
    // Levels and the console format come from appsettings (JSON in production, one line per
    // entry, which Fly keeps whole). Only what configuration can't express is set here.
    public static IServiceCollection AddAppLogging(this IServiceCollection services)
    {
        // Keep accents readable ("Échec", not "\u00C9chec") so `fly logs | grep` finds French text.
        services.Configure<JsonConsoleFormatterOptions>(options =>
            options.JsonWriterOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return services;
    }

    // Every log line written while handling a request carries who made it (scopes are printed
    // because Logging:Console:FormatterOptions:IncludeScopes is true). Staff account names
    // only — no member data (docs/journalisation.md).
    // Registered before the error handling, so the lines written by the exception handlers are
    // covered too; the user is read when a line is written, since authentication runs later.
    public static WebApplication UseUserLogScope(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ChurchAttendance.Request");
        app.Use(async (context, next) =>
        {
            var scope = new LogScope(() => UserFields(context.User));
            using (logger.BeginScope(scope))
            {
                try
                {
                    await next();
                }
                finally
                {
                    // The HttpContext is recycled after the request; a provider that keeps scopes
                    // (FakeLogger in tests) must not read it then.
                    scope.Freeze();
                }
            }
        });
        return app;
    }

    // Also used by AppComponentBase: Blazor events run on the circuit, outside any HTTP request.
    public static List<KeyValuePair<string, object?>> UserFields(ClaimsPrincipal? user)
    {
        var authenticated = user?.Identity?.IsAuthenticated == true;
        return
        [
            new("UserName", authenticated ? user!.Identity!.Name : "(anonyme)"),
            new("Role", authenticated ? user!.FindFirst(ClaimTypes.Role)?.Value : null),
        ];
    }
}

// A log scope with named fields: the JSON console formatter writes each field as a property,
// the simple one (development) prints ToString().
public sealed class LogScope(Func<List<KeyValuePair<string, object?>>> readFields) : IReadOnlyList<KeyValuePair<string, object?>>
{
    private Func<List<KeyValuePair<string, object?>>> _readFields = readFields;

    public LogScope(List<KeyValuePair<string, object?>> fields) : this(() => fields)
    {
    }

    private List<KeyValuePair<string, object?>> Fields => _readFields();

    // Keeps the current values from now on.
    public void Freeze()
    {
        var fields = _readFields();
        _readFields = () => fields;
    }

    public int Count => Fields.Count;
    public KeyValuePair<string, object?> this[int index] => Fields[index];
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => Fields.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => string.Join(" ", Fields.Select(f => $"{f.Key}={f.Value}"));
}
