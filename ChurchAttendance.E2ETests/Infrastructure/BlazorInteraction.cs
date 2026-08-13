using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ChurchAttendance.E2ETests.Infrastructure;

public static class BlazorInteraction
{
    // A freshly-navigated @rendermode InteractiveServer page renders its static markup
    // immediately, but the SignalR circuit that wires up @onclick handlers attaches slightly
    // later. A click fired in that window is silently dropped (the DOM click happens, no
    // server round trip occurs). Retrying the click until it visibly takes effect handles that
    // one-time startup race without a blind fixed delay; once a page's circuit is confirmed
    // connected by one successful interaction, later clicks on it need no retry.
    public static async Task ClickUntilVisibleAsync(IPage page, string clickSelector, ILocator expectVisible, int maxAttempts = 15)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await page.ClickAsync(clickSelector);
            try
            {
                await Expect(expectVisible).ToBeVisibleAsync(new() { Timeout = 500 });
                return;
            }
            catch (PlaywrightException) when (attempt < maxAttempts)
            {
            }
        }
    }
}
