using System.Net;
using ChurchAttendance.Infrastructure;
using ChurchAttendance.Infrastructure.Cards;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChurchAttendance.Tests.Endpoints;

// The church banner tops the member card, on the phone and on the printed PNG; the home-screen
// shortcut shows the church's arched-window logo.
public class MemberCardTests
{
    [Fact]
    public async Task CardPage_ShowsTheBanner_WhichAnyoneCanLoad()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Members.Add(new Member { FirstName = "Jean", LastName = "Dupont", Token = "jeton-carte", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var page = await anonymous.GetStringAsync("/card/jeton-carte");
        var banner = await anonymous.GetAsync("/branding/banniere-eglise.jpg");

        Assert.Contains("<img class=\"banner\" src=\"/branding/banniere-eglise.jpg\"", page);
        Assert.Equal(HttpStatusCode.OK, banner.StatusCode);
        Assert.Equal("image/jpeg", banner.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void PrintedCard_HasTheBannerOnTop_AndTheQrCodeBelowIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var webRoot = factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider;

        using var card = Image.Load<Rgba32>(CardImageService.GeneratePng("Jean Dupont", "https://eglise.example/card/abc", webRoot));
        using var banner = Image.Load<Rgba32>(Branding.ReadBanner(webRoot));

        Assert.Equal(600, card.Width);
        var bannerHeight = 600 * banner.Height / banner.Width;
        Assert.Equal(700 + bannerHeight, card.Height);
        // Across the banner's middle row, mostly its blue (not the card's white background).
        var blue = Enumerable.Range(0, card.Width).Count(x => card[x, bannerHeight / 2] is var p && p.B > p.R + 60);
        Assert.True(blue > card.Width / 2, $"{blue}/{card.Width} pixels bleus dans le bandeau");
        // The QR code moved down with the rest (420 px tall, 220 px below the banner): its
        // middle row has black modules, and the card ends below it.
        var qrMiddle = bannerHeight + 220 + 420 / 2;
        Assert.Contains(Enumerable.Range(150, 300), x => card[x, qrMiddle].R < 60);
        Assert.True(bannerHeight + 220 + 420 <= card.Height);
    }

    [Theory]
    [InlineData("/card/icon-180.png", 180)]
    [InlineData("/card/icon-192.png", 192)]
    [InlineData("/card/icon-512.png", 512)]
    public async Task ShortcutIcons_ShowTheOrangeArchOnNavy(string path, int size)
    {
        using var factory = new CustomWebApplicationFactory();
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        using var icon = Image.Load<Rgba32>(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal((size, size), (icon.Width, icon.Height));
        // The previous icons were plain black squares: check for both of the logo's colours.
        var pixels = Enumerable.Range(0, size * size).Select(i => icon[i % size, i / size]).ToList();
        Assert.Contains(pixels, p => p.R > 200 && p.G is > 80 and < 130 && p.B < 80);   // orange
        Assert.Contains(pixels, p => p.R < 20 && p.G < 40 && p.B is > 60 and < 110);    // navy
    }

    [Fact]
    public async Task CardPage_PointsIPhonesToTheShortcutIcon()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Members.Add(new Member { FirstName = "Jean", LastName = "Dupont", Token = "jeton-icone", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var page = await factory.CreateClient().GetStringAsync("/card/jeton-icone");

        Assert.Contains("<link rel=\"apple-touch-icon\" sizes=\"180x180\" href=\"/card/icon-180.png\">", page);
    }
}
