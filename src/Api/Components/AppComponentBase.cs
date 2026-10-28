using ChurchAttendance.Application.Errors;
using ChurchAttendance.Startup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace ChurchAttendance.Components;

// Base for interactive pages. User actions run through TryRunAsync: an unexpected failure
// (database unavailable, conflicting write...) then shows a French message in the page instead
// of killing the Blazor connection, and what the user typed stays on screen. Expected business
// failures are results, not exceptions, and never get here. See docs/gestion-des-erreurs.md.
public abstract class AppComponentBase : ComponentBase
{
    [Inject] private ILoggerFactory LoggerFactory { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IAuthorizationService AuthorizationService { get; set; } = default!;

    // For event handlers: whether the current user satisfies the policy (AuthorizationPolicies).
    // The matching button is hidden with <AuthorizeView Policy="...">; check again before acting.
    protected async Task<bool> IsAuthorizedAsync(string policy)
    {
        var user = (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User;
        return (await AuthorizationService.AuthorizeAsync(user, policy)).Succeeded;
    }

    // Runs the action. Returns null when it succeeded, otherwise the message to display,
    // starting with whatFailed (e.g. "Impossible de créer l'utilisateur").
    protected async Task<string?> TryRunAsync(Func<Task> action, string whatFailed)
    {
        var logger = LoggerFactory.CreateLogger(GetType());
        IDisposable? scope = null;
        try
        {
            // Blazor events run on the circuit, outside the HTTP request and its user scope
            // (LoggingSetup.UseUserLogScope): give every log written by the action the page and user.
            // Inside the try: a failure reading the user must not get past this boundary either.
            var user = (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User;
            var fields = LoggingSetup.UserFields(user);
            fields.Add(new("Page", GetType().Name));
            scope = logger.BeginScope(new LogScope(fields));

            await action();
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) // UI boundary (docs/gestion-des-erreurs.md)
        {
            // Logged here, once; the reference shown to the user is in the log line.
            var reference = Guid.NewGuid().ToString("N")[..8];
            logger.LogError(ex, "{WhatFailed} (page {Page}, réf. {Reference})", whatFailed, GetType().Name, reference);

            return ex switch
            {
                DomainException domain => $"{whatFailed} : {domain.Message}",
                ConflictException => $"{whatFailed} : cette donnée vient d'être modifiée par quelqu'un d'autre. Réessayez.",
                _ => $"{whatFailed} : une erreur inattendue est survenue. Réessayez dans un instant (réf. {reference}).",
            };
        }
        finally
        {
            scope?.Dispose();
        }
    }
}
