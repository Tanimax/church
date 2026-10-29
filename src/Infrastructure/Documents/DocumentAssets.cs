using System.Reflection;
using PdfSharp.Fonts;

namespace ChurchAttendance.Infrastructure.Documents;

// PDFsharp finds fonts through one process-wide resolver. The Docker image has no fonts, so the
// sheet's own font (Liberation Sans, SIL OFL, licence next to the files) ships inside this
// assembly. Every family asked for is answered with it.
internal sealed class DocumentAssets : IFontResolver
{
    public const string FamilyName = "Liberation Sans";
    private const string Regular = "LiberationSans-Regular";
    private const string Bold = "LiberationSans-Bold";

    private static readonly Lock RegistrationLock = new();
    private static bool _registered;

    // Must run before PDFsharp lays out any text; setting the resolver twice throws.
    public static void EnsureRegistered()
    {
        lock (RegistrationLock)
        {
            if (!_registered)
            {
                GlobalFontSettings.FontResolver = new DocumentAssets();
                _registered = true;
            }
        }
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? Bold : Regular);

    public byte[] GetFont(string faceName) => Resource($"{faceName}.ttf");

    private static byte[] Resource(string fileName)
    {
        var assembly = typeof(DocumentAssets).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".Assets.{fileName}", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
