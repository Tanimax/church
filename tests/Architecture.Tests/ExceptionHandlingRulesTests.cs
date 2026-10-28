using System.Text.RegularExpressions;

namespace ChurchAttendance.Architecture.Tests;

// Rules from docs/gestion-des-erreurs.md (§ 5), checked on the source code since they are
// about try/catch, which compiled-assembly rules can't see.
public class ExceptionHandlingRulesTests
{
    // The only places allowed to catch every exception, and why.
    private static readonly Dictionary<string, string> CatchAllAllowed = new()
    {
        ["src/Api/Components/AppComponentBase.cs"] = "frontière des actions de page interactive",
        ["src/Infrastructure/Email/EmailService.cs"] = "envoi d'e-mail au mieux : ne bloque jamais l'action",
    };

    private static readonly Regex CatchAll = new(@"catch\s*\(\s*(System\.)?Exception\b", RegexOptions.Compiled);
    private static readonly Regex BareCatch = new(@"catch\s*(\{|$)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex EmptyCatch = new(@"catch\s*(\([^)]*\))?\s*(when\s*\([^)]*\)\s*)?\{\s*\}", RegexOptions.Compiled);

    [Fact]
    public void CatchAll_OnlyAtTheDocumentedBoundaries()
    {
        var offenders = SourceCode.Files()
            .Where(f => !CatchAllAllowed.ContainsKey(f.Path))
            .Where(f => CatchAll.IsMatch(f.Text) || BareCatch.IsMatch(f.Text))
            .Select(f => f.Path)
            .ToList();

        Assert.True(offenders.Count == 0,
            "catch (Exception) hors des frontières documentées : " + string.Join(", ", offenders));
    }

    [Fact]
    public void NoEmptyCatch()
    {
        var offenders = SourceCode.Files().Where(f => EmptyCatch.IsMatch(f.Text)).Select(f => f.Path).ToList();

        Assert.True(offenders.Count == 0, "catch vide : " + string.Join(", ", offenders));
    }

    // An interactive page's actions must be able to fail without killing the Blazor
    // connection: they go through AppComponentBase.TryRunAsync.
    [Fact]
    public void InteractivePages_InheritAppComponentBase()
    {
        var offenders = SourceCode.Files()
            .Where(f => f.Path.EndsWith(".razor") && f.Text.Contains("@rendermode InteractiveServer"))
            .Where(f => !f.Text.Contains("@inherits AppComponentBase"))
            .Select(f => f.Path)
            .ToList();

        Assert.True(offenders.Count == 0,
            "pages interactives qui n'héritent pas d'AppComponentBase : " + string.Join(", ", offenders));
    }
}
