using System.IO.Compression;

namespace ChurchAttendance.Services;

public static class MemberCardZipService
{
    public static byte[] BuildZip(IEnumerable<(string FullName, string CardUrl)> members, string webRootPath)
    {
        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Members can share the exact same name — track used filenames so a second
            // "Jean Dupont" doesn't silently overwrite the first one in the zip.
            var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var (fullName, cardUrl) in members)
            {
                var png = CardImageService.GeneratePng(fullName, cardUrl, webRootPath);
                var fileName = BuildUniqueFileName(fullName, usedNames);

                var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(png);
            }
        }

        return zipStream.ToArray();
    }

    private static string BuildUniqueFileName(string fullName, Dictionary<string, int> usedNames)
    {
        var sanitized = string.Join(' ', fullName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "membre";
        }

        var count = usedNames.GetValueOrDefault(sanitized, 0) + 1;
        usedNames[sanitized] = count;

        return count == 1 ? $"{sanitized}.png" : $"{sanitized} ({count}).png";
    }
}
