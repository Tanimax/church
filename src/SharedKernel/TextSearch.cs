using System.Globalization;

namespace ChurchAttendance.SharedKernel;

// Name matching for the search boxes: "eloise" finds "Éloïse", "JEAN" finds "Jean",
// "jean pierre" finds "Jean-Pierre". French names carry accents and hyphens that people
// often skip when typing, especially on a phone.
public static class TextSearch
{
    private static readonly CompareInfo Comparer = CultureInfo.InvariantCulture.CompareInfo;
    // IgnoreSymbols also skips spaces and punctuation: "jean pierre", "jean-pierre" and
    // "jeanpierre" are the same search.
    private const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreSymbols;

    // True when term is empty (no filter) or appears in text.
    public static bool Contains(string? text, string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }
        return text is not null && Comparer.IndexOf(text, term.Trim(), Loose) >= 0;
    }

    // "Adolescents", " adolescents " and "Préados" / "pre-ados" are the same name.
    public static bool SameText(string? a, string? b) =>
        Comparer.Compare(a?.Trim() ?? "", b?.Trim() ?? "", Loose) == 0;
}
