using System.Net;
using System.Text.RegularExpressions;
using ChurchAttendance.Components.Layout;
using ChurchAttendance.Tests.Infrastructure;
using Microsoft.AspNetCore.Components.Routing;

namespace ChurchAttendance.Tests.Endpoints;

// The admin menu groups the reports under "Rapports" and what the church communicates under
// "Communication"; a breadcrumb above each page says where it sits in that menu.
public class NavigationTests
{
    [Theory]
    [InlineData("admin/reports", new[] { "Accueil", "Rapports", "Dominical" })]
    [InlineData("admin/reports/sainte-cene", new[] { "Accueil", "Rapports", "Sainte Cène" })]
    [InlineData("admin/reports/visiteurs?annee=2026", new[] { "Accueil", "Rapports", "Visiteurs" })]
    [InlineData("admin/reports/anniversaires", new[] { "Accueil", "Rapports", "Anniversaires" })]
    [InlineData("admin/programmation", new[] { "Accueil", "Communication", "Programmation" })]
    [InlineData("admin/annonces", new[] { "Accueil", "Communication", "Annonces" })]
    [InlineData("admin/members/", new[] { "Accueil", "Membres" })]
    public void Trail_FollowsTheMenu(string path, string[] titles)
    {
        var trail = SiteMap.Trail(path);

        Assert.Equal(titles, trail.Select(c => c.Title));
        Assert.Equal("/", trail[0].Href);
        Assert.All(trail.Skip(1), c => Assert.Null(c.Href)); // groups have no page of their own
    }

    [Theory]
    [InlineData("")]
    [InlineData("admin/login")]
    [InlineData("not-found")]
    public void Trail_IsEmpty_OnHomeAndOffMenuPages(string path)
    {
        Assert.Empty(SiteMap.Trail(path));
    }

    [Fact]
    public void SundayReportLink_IsOnlyActiveOnItsOwnPage()
    {
        // Otherwise "Dominical" would light up on every page under admin/reports/.
        var pages = SiteMap.Menu.OfType<NavGroup>().SelectMany(g => g.Pages).ToList();
        Assert.Equal(NavLinkMatch.All, SiteMap.MatchFor(pages.Single(p => p.Href == "admin/reports")));
        Assert.Equal(NavLinkMatch.Prefix, SiteMap.MatchFor(pages.Single(p => p.Href == "admin/reports/visiteurs")));
    }

    [Fact]
    public async Task ReportPage_ShowsItsBreadcrumb_WithTheReportsGroupOpen()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var html = Clean(await client.GetStringAsync("/admin/reports/visiteurs"));

        Assert.Matches(
            @"<nav aria-label=""Fil d[^""]*Ariane"" class=""app-breadcrumb""><ol class=""breadcrumb"">"
            + @"<li class=""breadcrumb-item""><a href=""/"">Accueil</a></li>"
            + @"<li class=""breadcrumb-item"">Rapports</li>"
            + @"<li class=""breadcrumb-item active"" aria-current=""page"">Visiteurs</li></ol></nav>",
            html);
        var groups = Groups(html);
        Assert.True(groups["Rapports"].Open);
        Assert.Equal(["Sainte Cène", "Visiteurs", "Anniversaires"], groups["Rapports"].Links);
        Assert.False(groups["Communication"].Open);
        Assert.Equal(["Programmation", "Annonces"], groups["Communication"].Links);
    }

    [Theory]
    [InlineData("/admin/ecole-dominicale", "École Dominicale")]
    [InlineData("/admin/reports", "Rapport Dominical")]
    [InlineData("/admin/sainte-cene-manuelle", "Sainte Cène manuelle")]
    public async Task UnusedPages_AreOutOfTheMenu_ButStillOpenFromTheirAddress(string path, string heading)
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var menu = Clean(await client.GetStringAsync("/admin/members"));
        var page = await client.GetAsync(path);

        Assert.DoesNotContain($"href=\"{path.TrimStart('/')}\"", menu);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Matches($"<h1[^>]*>{heading}</h1>", WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task HomePage_HasNoBreadcrumb()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory);

        var html = await client.GetStringAsync("/");

        Assert.DoesNotContain("app-breadcrumb", html);
    }

    [Theory]
    [InlineData(UserRole.Secretaire, true)]
    [InlineData(UserRole.EcoleDominicale, false)]
    public async Task CommunicationGroup_IsShownToTheSecretariatOnly(UserRole role, bool shown)
    {
        using var factory = new CustomWebApplicationFactory();
        await factory.SeedUserAsync("compte", "motdepasse123", role);
        var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "compte", "motdepasse123");

        var html = Clean(await client.GetStringAsync("/admin/members"));

        var groups = Groups(html);
        Assert.Contains("Rapports", groups.Keys);
        Assert.Equal(shown, groups.ContainsKey("Communication"));
        Assert.Equal(shown, html.Contains("href=\"admin/programmation\""));
    }

    // Drops the scoped-CSS attributes (b-xxxxxxxxxx) and the whitespace between tags, so the
    // markup can be compared as written.
    private static string Clean(string html) =>
        Regex.Replace(Regex.Replace(html, @"\s+b-[a-z0-9]{10}(="""")?", ""), @">\s+<", "><");

    private static Dictionary<string, (bool Open, List<string> Links)> Groups(string html) =>
        Regex.Matches(html, @"<details class=""nav-group[^""]*""(?<open> open(="""")?)?>(?<body>.*?)</details>", RegexOptions.Singleline)
            .ToDictionary(
                m => WebUtility.HtmlDecode(Regex.Match(m.Groups["body"].Value, @"</span>\s*([^<]+?)\s*</summary>").Groups[1].Value),
                m => (m.Groups["open"].Success,
                      Regex.Matches(m.Groups["body"].Value, @"<a[^>]*class=""nav-link[^""]*""[^>]*>\s*([^<]+?)\s*</a>")
                          .Select(l => WebUtility.HtmlDecode(l.Groups[1].Value)).ToList()));
}
