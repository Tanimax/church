using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ChurchAttendance.Services;

public static class CardImageService
{
    private const int Width = 600;
    private const int Height = 700;
    private const int QrSize = 420;

    // Docker's Linux base image has no system fonts installed, so the font is bundled with the
    // app instead of relying on SystemFonts (which would throw at render time). The path comes
    // from IWebHostEnvironment.WebRootPath rather than AppContext.BaseDirectory — the static web
    // assets pipeline doesn't physically copy wwwroot/ next to the DLL at build time (only at
    // publish time), so a BaseDirectory-relative path works in the deployed container but not
    // when running the build output directly.
    private static FontFamily? _family;

    private static FontFamily GetFamily(string webRootPath)
    {
        return _family ??= new FontCollection()
            .Add(System.IO.Path.Combine(webRootPath, "fonts", "PublicSans-Variable.ttf"));
    }

    public static byte[] GeneratePng(string fullName, string cardUrl, string webRootPath)
    {
        var family = GetFamily(webRootPath);
        var labelFont = family.CreateFont(20, FontStyle.Regular);
        var nameFont = family.CreateFont(38, FontStyle.Bold);

        using var image = new Image<Rgba32>(Width, Height, Color.White);

        var labelOptions = new RichTextOptions(labelFont)
        {
            Origin = new PointF(Width / 2f, 55),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var nameOptions = new RichTextOptions(nameFont)
        {
            Origin = new PointF(Width / 2f, 110),
            HorizontalAlignment = HorizontalAlignment.Center,
            WrappingLength = Width - 80,
            TextAlignment = TextAlignment.Center
        };

        image.Mutate(ctx => ctx
            .Fill(Color.White)
            .Draw(Color.Gainsboro, 2f, new RectangularPolygon(1, 1, Width - 2, Height - 2))
            .DrawText(labelOptions, "CARTE DE MEMBRE", Color.Gray)
            .DrawText(nameOptions, fullName, Color.Black));

        var qrBytes = QrCodeService.GeneratePng(cardUrl, pixelsPerModule: 12);
        using (var qrImage = Image.Load(qrBytes))
        {
            qrImage.Mutate(ctx => ctx.Resize(QrSize, QrSize));
            var qrPosition = new Point((Width - QrSize) / 2, 220);
            image.Mutate(ctx => ctx.DrawImage(qrImage, qrPosition, 1f));
        }

        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }
}
