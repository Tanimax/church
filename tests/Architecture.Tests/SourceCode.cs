namespace ChurchAttendance.Architecture.Tests;

// The .cs and .razor files under src/, for rules that compiled-assembly checks can't see.
internal static class SourceCode
{
    public static IEnumerable<(string Path, string Text)> Files()
    {
        var root = RepositoryRoot();
        return Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".cs") || p.EndsWith(".razor"))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(p => (Path.GetRelativePath(root, p).Replace('\\', '/'), File.ReadAllText(p)));
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.sln").Any())
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Racine du dépôt introuvable (pas de .sln).");
    }
}
