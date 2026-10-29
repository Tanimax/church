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

// The church banner tops the member card, on the phone and on the printed PNG.
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
}
