using ChurchAttendance.Application.Errors;
using Microsoft.AspNetCore.Components;

namespace ChurchAttendance.Components;

// Base for interactive pages. User actions run through TryRunAsync: an unexpected failure
// (database unavailable, conflicting write...) then shows a French message in the page instead
// of killing the Blazor connection, and what the user typed stays on screen. Expected business
// failures are results, not exceptions, and never get here. See docs/gestion-des-erreurs.md.
public abstract class AppComponentBase : ComponentBase
{
    [Inject] private ILoggerFactory LoggerFactory { get; set; } = default!;

    // Runs the action. Returns null when it succeeded, otherwise the message to display,
    // starting with whatFailed (e.g. "Impossible de créer l'utilisateur").
    protected async Task<string?> TryRunAsync(Func<Task> action, string whatFailed)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) // UI boundary (docs/gestion-des-erreurs.md)
        {
            // Logged here, once; the reference shown to the user is in the log line.
            var reference = Guid.NewGuid().ToString("N")[..8];
            LoggerFactory.CreateLogger(GetType()).LogError(
                ex, "{WhatFailed} (page {Page}, réf. {Reference})", whatFailed, GetType().Name, reference);

            return ex switch
            {
                DomainException domain => $"{whatFailed} : {domain.Message}",
                ConflictException => $"{whatFailed} : cette donnée vient d'être modifiée par quelqu'un d'autre. Réessayez.",
                _ => $"{whatFailed} : une erreur inattendue est survenue. Réessayez dans un instant (réf. {reference}).",
            };
        }
    }
}
