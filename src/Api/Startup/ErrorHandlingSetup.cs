using System.Diagnostics;
using ChurchAttendance.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;

namespace ChurchAttendance.Startup;

// The app's error boundaries for HTTP (see docs/gestion-des-erreurs.md):
// - /api/*: every failure, thrown or returned as a bare status code, becomes a ProblemDetails
//   JSON body with a French title and the request id, so the scanner can say what went wrong.
// - pages: unhandled exceptions show the French /Error page, unknown URLs the /not-found page.
// Exceptions are logged once: by ApiExceptionHandler for /api/* (since .NET 10 the middleware
// no longer logs what an IExceptionHandler handled), by the exception handler middleware for pages.
public static class ErrorHandlingSetup
{
    public static IServiceCollection AddAppErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] = RequestId(context.HttpContext);
            // Bare status codes (no exception) otherwise get ASP.NET Core's English titles.
            if (context.Exception is null && FrenchTitles.TryGetValue(context.ProblemDetails.Status ?? 0, out var title))
            {
                context.ProblemDetails.Title = title;
            }
        });
        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }

    public static WebApplication UseAppErrorHandling(this WebApplication app)
    {
        // Pages. Re-executing /not-found needs to be in the main pipeline (it doesn't work from
        // a UseWhen branch), so these apply to every request...
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

        // ...and API requests are handled first, by these inner middlewares, in every
        // environment: they write a ProblemDetails body, so the page handlers above leave the
        // response alone.
        app.UseWhen(IsApiRequest, api =>
        {
            api.UseExceptionHandler();
            api.UseStatusCodePages();
        });
        return app;
    }

    private static readonly Dictionary<int, string> FrenchTitles = new()
    {
        [StatusCodes.Status400BadRequest] = "Requête invalide.",
        [StatusCodes.Status401Unauthorized] = "Connexion requise.",
        [StatusCodes.Status403Forbidden] = "Accès refusé.",
        [StatusCodes.Status404NotFound] = "Introuvable.",
        [StatusCodes.Status405MethodNotAllowed] = "Méthode non autorisée.",
        [StatusCodes.Status415UnsupportedMediaType] = "Format de requête non pris en charge.",
    };

    public static bool IsApiRequest(HttpContext context) => context.Request.Path.StartsWithSegments("/api");

    // Shown to the user; the same value is the "TraceId" of every log line of the request
    // (Logging:Console:FormatterOptions:IncludeScopes), so `fly logs | grep <référence>` finds them.
    public static string RequestId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
}

internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (!ErrorHandlingSetup.IsApiRequest(context))
        {
            return false; // pages: the /Error page
        }

        var (status, title) = exception switch
        {
            BadHttpRequestException bad => (bad.StatusCode, "Requête invalide."),
            DomainException domain => (StatusCodes.Status400BadRequest, domain.Message),
            ConflictException => (StatusCodes.Status409Conflict,
                "Cette donnée vient d'être modifiée par quelqu'un d'autre. Réessayez."),
            RetryLimitExceededException or TimeoutException => (StatusCodes.Status503ServiceUnavailable,
                "Le service est momentanément indisponible. Réessayez dans un instant."),
            _ => (StatusCodes.Status500InternalServerError,
                "Une erreur inattendue est survenue. Réessayez dans un instant."),
        };

        // Since .NET 10 the exception handler middleware no longer logs what an IExceptionHandler
        // handles: this line is the only trace of the failure. Client errors are expected.
        // The route pattern, not the path: /api/members/{token}/qr.png must not log the card token.
        // (The middleware clears the endpoint before calling handlers; the feature keeps it.)
        var endpoint = context.Features.Get<IExceptionHandlerFeature>()?.Endpoint;
        var route = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "(route inconnue)";
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Échec de {Method} {Route} : {Status}", context.Request.Method, route, status);
        }
        else
        {
            logger.LogInformation("Requête refusée {Method} {Route} : {Status} {Title}", context.Request.Method, route, status, title);
        }

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
        });
    }
}
