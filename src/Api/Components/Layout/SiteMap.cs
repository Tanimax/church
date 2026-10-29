using ChurchAttendance.Startup;
using Microsoft.AspNetCore.Components.Routing;

namespace ChurchAttendance.Components.Layout;

public abstract record NavEntry(string Title, string Icon);

// Policy or Roles decide who sees the link (neither: any signed-in user). The page itself
// still checks access: a hidden link is not a security boundary.
// Hidden: left out of the menu while the church doesn't use the page; it still works from its
// address and keeps its breadcrumb, so showing it again is a one-word change.
public sealed record NavPage(string Title, string Href, string Icon = "", string? Policy = null, string? Roles = null, bool Hidden = false)
    : NavEntry(Title, Icon);

public sealed record NavGroup(string Title, string Icon, IReadOnlyList<NavPage> Pages, string? Policy = null)
    : NavEntry(Title, Icon);

public sealed record Crumb(string Title, string? Href);

// The admin menu and the breadcrumb are both built from this one list, so a page moved to
// another group moves in both places at once.
public static class SiteMap
{
    public static readonly NavPage Home = new("Accueil", "", "house-door-fill");

    public static readonly IReadOnlyList<NavEntry> Menu =
    [
        Home,
        new NavGroup("Rapports", "bar-chart-fill",
        [
            new NavPage("Dominical", "admin/reports", Hidden: true),
            new NavPage("Sainte Cène", "admin/reports/sainte-cene"),
            new NavPage("Visiteurs", "admin/reports/visiteurs"),
            new NavPage("Anniversaires", "admin/reports/anniversaires"),
        ]),
        new NavPage("Sainte Cène manuelle", "admin/sainte-cene-manuelle", "person-check-fill", Roles: "Admin,Secretaire", Hidden: true),
        new NavGroup("Communication", "megaphone-fill",
        [
            new NavPage("Programmation", "admin/programmation"),
            new NavPage("Annonces", "admin/annonces"),
        ], Policy: AuthorizationPolicies.ManageCommunication),
        new NavPage("Membres", "admin/members", "people-fill"),
        new NavPage("École Dominicale", "admin/ecole-dominicale", "mortarboard-fill", Hidden: true),
        new NavPage("Utilisateurs", "admin/users", "person-badge-fill"),
    ];

    private static IEnumerable<(NavGroup? Group, NavPage Page)> AllPages() =>
        Menu.SelectMany(entry => entry switch
        {
            NavGroup group => group.Pages.Select(page => ((NavGroup?)group, page)),
            NavPage page => [((NavGroup?)null, page)],
            _ => [],
        });

    // "Accueil > Rapports > Sainte Cène" for admin/reports/sainte-cene. Empty for a page that
    // isn't in the menu (login, errors) and for the home page itself, which has nowhere to go
    // back to.
    public static IReadOnlyList<Crumb> Trail(string relativePath)
    {
        var path = relativePath.Split('?', '#')[0].Trim('/');
        if (path.Length == 0)
        {
            return [];
        }

        var match = AllPages().FirstOrDefault(p => string.Equals(p.Page.Href, path, StringComparison.OrdinalIgnoreCase));
        if (match.Page is null)
        {
            return [];
        }

        var trail = new List<Crumb> { new(Home.Title, "/") };
        if (match.Group is not null)
        {
            trail.Add(new Crumb(match.Group.Title, null));
        }
        trail.Add(new Crumb(match.Page.Title, null));
        return trail;
    }

    public static bool Contains(NavGroup group, string relativePath)
    {
        var path = relativePath.Split('?', '#')[0].Trim('/');
        return group.Pages.Any(p => string.Equals(p.Href, path, StringComparison.OrdinalIgnoreCase));
    }

    // "admin/reports" must not light up on admin/reports/visiteurs: a link whose address
    // starts another link's address only matches exactly.
    public static NavLinkMatch MatchFor(NavPage page) =>
        page.Href.Length == 0 || AllPages().Any(p => p.Page.Href.StartsWith(page.Href + "/", StringComparison.Ordinal))
            ? NavLinkMatch.All
            : NavLinkMatch.Prefix;
}
