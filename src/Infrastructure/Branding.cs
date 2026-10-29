namespace ChurchAttendance.Infrastructure;

// The church's banner ("Église De Dieu De Béthel"), one file for every place it appears: the
// member card page (served as /branding/banniere-eglise.jpg), the printed card PNG and the
// quarterly schedule PDF. Read from the web root like the card font (see CardImageService).
public static class Branding
{
    public static string BannerPath(string webRootPath) =>
        Path.Combine(webRootPath, "branding", "banniere-eglise.jpg");
}
