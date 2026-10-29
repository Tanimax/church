using Microsoft.Extensions.FileProviders;

namespace ChurchAttendance.Infrastructure;

// The church's banner ("Église De Dieu De Béthel"), at the top of the quarterly schedule PDF.
public static class Branding
{
    public const string BannerFile = "branding/banniere-eglise.jpg";

    public static byte[] ReadBanner(IFileProvider webRoot) => WebRootFiles.Read(webRoot, BannerFile);
}

// Reads a file of wwwroot through IWebHostEnvironment.WebRootFileProvider, never a disk path:
// when the app runs from its build output (end-to-end tests), wwwroot isn't copied next to the
// DLL — its files are served from the project folder ("static web assets") and WebRootPath is
// null. The provider finds them there as well as in the published app.
internal static class WebRootFiles
{
    public static byte[] Read(IFileProvider webRoot, string path)
    {
        var file = webRoot.GetFileInfo(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException($"wwwroot/{path} est introuvable.", path);
        }
        using var stream = file.CreateReadStream();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
