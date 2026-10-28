using Microsoft.AspNetCore.Http;

namespace ChurchAttendance.SharedKernel;

// Single place that knows the shape of the app's public URLs (member cards, visitor form),
// so endpoints and Blazor pages stop rebuilding "{scheme}://{host}/card/{token}" by hand.
public static class PublicUrls
{
    // Base URL of the current request. Relies on UseFlyForwardedHeaders having run so the
    // scheme is the original https, not the plain http Fly forwards to the container.
    public static string BaseUrl(HttpRequest request) => $"{request.Scheme}://{request.Host}";

    public static string Card(string baseUrl, string token) => $"{baseUrl.TrimEnd('/')}/card/{token}";

    public static string Card(HttpRequest request, string token) => Card(BaseUrl(request), token);

    public static string VisitorForm(string baseUrl) => $"{baseUrl.TrimEnd('/')}/visitor";

    public static string VisitorForm(HttpRequest request) => VisitorForm(BaseUrl(request));
}
