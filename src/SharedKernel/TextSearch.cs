using System.Globalization;

namespace ChurchAttendance.SharedKernel;

// Name matching for the search boxes: "eloise" finds "Éloïse", "JEAN" finds "Jean". French
// names carry accents that people often skip when typing, especially on a phone.
public static class TextSearch
{
    private static readonly CompareInfo Comparer = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions IgnoreCaseAndAccents = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    // True when term is empty (no filter) or appears in text.
    public static bool Contains(string? text, string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }
        return text is not null && Comparer.IndexOf(text, term.Trim(), IgnoreCaseAndAccents) >= 0;
    }

    // "Adolescents" and " adolescents " are the same name.
    public static bool SameText(string? a, string? b) =>
        Comparer.Compare(a?.Trim() ?? "", b?.Trim() ?? "", IgnoreCaseAndAccents) == 0;
}
