using System.Collections.Generic;
using System.IO;

namespace D2Net.Init;

public sealed record DartFileEntry(string Filename, string FullPath);

/// <summary>
/// FR-014: walks the source tree skipping every approved excluded directory.
/// Returns one record per <c>.dart</c> file with the bare filename and the
/// full path relative to the repo root, using forward-slash separators on
/// every host OS.
/// </summary>
public static class DartFileScanner
{
    public static IReadOnlyList<DartFileEntry> Scan(
        string repoRoot,
        string sourceDirName,
        IEnumerable<string> excludedRelPaths)
    {
        var sourceAbs = Path.Combine(repoRoot, sourceDirName);
        if (!Directory.Exists(sourceAbs)) return Array.Empty<DartFileEntry>();

        var excludedAbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in excludedRelPaths)
        {
            if (string.IsNullOrWhiteSpace(rel)) continue;
            var native = rel.Replace('/', Path.DirectorySeparatorChar);
            excludedAbs.Add(Path.GetFullPath(Path.Combine(sourceAbs, native)));
        }

        var results = new List<DartFileEntry>();
        Walk(repoRoot, sourceAbs, excludedAbs, results);
        results.Sort((a, b) => string.CompareOrdinal(a.FullPath, b.FullPath));
        return results;
    }

    private static void Walk(string repoRoot, string dir, HashSet<string> excludedAbs, List<DartFileEntry> results)
    {
        // Files in this dir
        foreach (var f in Directory.EnumerateFiles(dir))
        {
            if (Path.GetExtension(f).Equals(".dart", StringComparison.OrdinalIgnoreCase))
            {
                var rel = Path.GetRelativePath(repoRoot, f).Replace('\\', '/');
                results.Add(new DartFileEntry(Path.GetFileName(f), rel));
            }
        }
        // Subdirs
        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            var subAbs = Path.GetFullPath(sub);
            if (excludedAbs.Contains(subAbs)) continue;
            Walk(repoRoot, sub, excludedAbs, results);
        }
    }
}
