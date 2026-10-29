using System.Net;
using ChurchAttendance.Infrastructure.Cards;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChurchAttendance.Tests.Endpoints;

// The member card stays plain white, without the church banner (that one is for the schedule
// PDF); the home-screen shortcut shows the church's arched-window logo.
public class MemberCardTests
{
    [Fact]
    public async Task CardPage_HasNoBanner()
    {
        using var factory = new CustomWebApplicationFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.Members.Add(new Member { FirstName = "Jean", LastName = "Dupont", Token = "jeton-carte", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var page = await anonymous.GetStringAsync("/card/jeton-carte");
        var notFound = await anonymous.GetAsync("/card/inconnu");

        Assert.Contains("<h1>Jean Dupont</h1>", page);
        Assert.DoesNotContain("banniere-eglise", page);
        Assert.DoesNotContain("banniere-eglise", await notFound.Content.ReadAsStringAsync());
    }

    [Fact]
    public void PrintedCard_IsWhiteOnTop_WithTheQrCodeBelowTheName()
    {
        using var factory = new CustomWebApplicationFactory();
        var webRoot = factory.Services.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider;

        using var card = Image.Load<Rgba32>(CardImageService.GeneratePng("Jean Dupont", "https://eglise.example/card/abc", webRoot));

        Assert.Equal((600, 700), (card.Width, card.Height));
        // No banner: above the "CARTE DE MEMBRE" label, the card is white inside its border.
        Assert.All(Enumerable.Range(10, card.Width - 20), x => Assert.Equal(new Rgba32(255, 255, 255), card[x, 20]));
        // The QR code (420 px tall, 220 px from the top): its middle row has black modules.
        Assert.Contains(Enumerable.Range(150, 300), x => card[x, 220 + 420 / 2].R < 60);
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
