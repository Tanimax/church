using ChurchAttendance.SharedKernel;
using Microsoft.Extensions.FileProviders;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;

namespace ChurchAttendance.Infrastructure.Cards;

public static class CardImageService
{
    private const int Width = 600;
    private const int QrSize = 420;

    // The church banner across the top (756x72, scaled to the card's width); everything else
    // sits below it.
    private const int BannerHeight = Width * 72 / 756;
    private const int Height = 700 + BannerHeight;

    // Docker's Linux base image has no system fonts installed, so the font is bundled with the
    // app (wwwroot/fonts) instead of relying on SystemFonts (which would throw at render time).
    // Like the banner it is read through the web root's file provider (see WebRootFiles).
    private static FontFamily? _family;

    private static FontFamily GetFamily(IFileProvider webRoot)
    {
        if (_family is null)
        {
            using var font = new MemoryStream(WebRootFiles.Read(webRoot, "fonts/PublicSans-Variable.ttf"));
            _family = new FontCollection().Add(font);
        }
        return _family.Value;
    }

    public static byte[] GeneratePng(string fullName, string cardUrl, IFileProvider webRoot)
    {
        var family = GetFamily(webRoot);
        var labelFont = family.CreateFont(20, FontStyle.Regular);
        var nameFont = family.CreateFont(38, FontStyle.Bold);

        using var image = new Image<Rgba32>(Width, Height, Color.White);

        var labelOptions = new RichTextOptions(labelFont)
        {
            Origin = new PointF(Width / 2f, BannerHeight + 55),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var nameOptions = new RichTextOptions(nameFont)
        {
            Origin = new PointF(Width / 2f, BannerHeight + 110),
            HorizontalAlignment = HorizontalAlignment.Center,
            WrappingLength = Width - 80,
            TextAlignment = TextAlignment.Center
        };

        using (var banner = Image.Load(Branding.ReadBanner(webRoot)))
        {
            banner.Mutate(ctx => ctx.Resize(Width, BannerHeight));
            image.Mutate(ctx => ctx.DrawImage(banner, new Point(0, 0), 1f));
        }

        image.Mutate(ctx => ctx
            .Draw(Color.Gainsboro, 2f, new RectangularPolygon(1, 1, Width - 2, Height - 2))
            .DrawText(labelOptions, "CARTE DE MEMBRE", Color.Gray)
            .DrawText(nameOptions, fullName, Color.Black));

        var qrBytes = QrCodeService.GeneratePng(cardUrl, pixelsPerModule: 12);
        using (var qrImage = Image.Load(qrBytes))
        {
            qrImage.Mutate(ctx => ctx.Resize(QrSize, QrSize));
            var qrPosition = new Point((Width - QrSize) / 2, BannerHeight + 220);
            image.Mutate(ctx => ctx.DrawImage(qrImage, qrPosition, 1f));
        }

        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }
}
