using System.Collections.Generic;
using System.IO;

namespace D2Net.Init;

/// <summary>
/// FR-006 + FR-007: builds the suggested exclusion list — well-known tool
/// subdirectories that exist anywhere in the source tree, plus directories
/// whose leaf name matches an archive/backup/old marker substring (R4).
/// </summary>
public static class ExclusionDetector
{
    /// <summary>R8: minimum-required first five plus a slightly extended set.</summary>
    public static readonly IReadOnlyList<string> WellKnownToolDirNames = new[]
    {
        ".git", ".dart_tool", "build", ".idea", ".vscode",
        "node_modules", "bin", "obj", ".gradle", ".next",
        ".pytest_cache", ".venv", "venv", ".nuget", ".terraform",
    };

    /// <summary>R4: case-insensitive substring markers tested against leaf name.</summary>
    public static readonly IReadOnlyList<string> ArchiveMarkers = new[]
    {
        "archive", "archives",
        "backup", "backups", "bak",
        "old", "legacy",
        "obsolete", "deprecated",
        "attic",
    };

    /// <summary>
    /// Walks <paramref name="sourceDir"/> recursively and returns the proposed
    /// list. Paths are forward-slash relative to <paramref name="sourceDir"/>.
    /// Manual exclusions supplied via CLI flags are appended (with kind = Manual)
    /// after the auto-detected ones; duplicates are de-duplicated keeping the
    /// highest-specificity kind (Tool &gt; Pattern &gt; Manual).
    /// </summary>
    public static IReadOnlyList<ProposedExclusion> Detect(
        string sourceDir,
        IEnumerable<string> manualExclusions)
    {
        var byPath = new Dictionary<string, ProposedExclusion>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(sourceDir))
        {
            foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var leaf = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(leaf)) continue;
                var rel = ToRelForward(sourceDir, dir);

                if (IsWellKnownToolDir(leaf))
                {
                    Upsert(byPath, new ProposedExclusion(
                        rel, ExclusionKind.Tool, $"well-known tool directory '{leaf}'"));
                }
                else if (TryMatchArchiveMarker(leaf, out var marker))
                {
                    Upsert(byPath, new ProposedExclusion(
                        rel, ExclusionKind.Pattern, $"matches archive marker '{marker}'"));
                }
            }
        }

        foreach (var raw in manualExclusions)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var rel = NormaliseRel(raw);
            Upsert(byPath, new ProposedExclusion(rel, ExclusionKind.Manual, "supplied via --exclude"));
        }

        var ordered = new List<ProposedExclusion>(byPath.Values);
        ordered.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return ordered;
    }

    public static bool IsWellKnownToolDir(string leafName)
    {
        foreach (var n in WellKnownToolDirNames)
            if (string.Equals(leafName, n, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool TryMatchArchiveMarker(string leafName, out string marker)
    {
        var lower = leafName.ToLowerInvariant();
        foreach (var m in ArchiveMarkers)
        {
            if (lower.Contains(m, StringComparison.Ordinal))
            {
                marker = m;
                return true;
            }
        }
        marker = string.Empty;
        return false;
    }

    private static void Upsert(Dictionary<string, ProposedExclusion> map, ProposedExclusion item)
    {
        if (map.TryGetValue(item.Path, out var existing))
        {
            // Keep highest specificity (Tool > Pattern > Manual).
            if (Specificity(item.Kind) > Specificity(existing.Kind))
                map[item.Path] = item;
        }
        else
        {
            map[item.Path] = item;
        }
    }

    private static int Specificity(ExclusionKind k) => k switch
    {
        ExclusionKind.Tool => 3,
        ExclusionKind.Pattern => 2,
        ExclusionKind.Manual => 1,
        _ => 0,
    };

    private static string ToRelForward(string root, string abs)
    {
        var rel = Path.GetRelativePath(root, abs);
        return rel.Replace('\\', '/');
    }

    private static string NormaliseRel(string raw)
    {
        var s = raw.Replace('\\', '/').Trim('/');
        return s;
    }
}
