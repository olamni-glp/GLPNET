using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace D2Net.Init;

/// <summary>
/// Stateless path validation and classification for the incremental
/// --add-exclude mode (feature 007).
///
/// All operations are pure functions of the inputs. The validator does
/// not touch the database; the runner threads the current
/// <c>excluded_directories</c> snapshot into <see cref="ClassifyAgainstExisting"/>.
/// </summary>
public static class PathValidator
{
    /// <summary>
    /// Suffixes that strongly suggest a path names a file rather than a
    /// directory. Used as a fall-back filter when the path does not exist
    /// at validation time. Research R2 — extensionless names (Makefile,
    /// Dockerfile, LICENSE) are not in this list and pass through.
    /// </summary>
    public static readonly IReadOnlyList<string> LikelyFileSuffixes = new[]
    {
        ".dart", ".zip", ".exe", ".dll", ".so", ".dylib",
        ".json", ".txt", ".md", ".lock",
    };

    /// <summary>
    /// Canonicalise the user-supplied raw path:
    ///   - convert backslashes to forward slashes
    ///   - strip trailing slashes (but keep at least the leaf)
    ///   - collapse repeated slashes
    ///   - reject empty after trim
    /// Returned form is suitable for source-root checks but is NOT yet
    /// resolved against the source root or any `..` segments.
    /// </summary>
    public static string Canonicalise(string raw)
    {
        if (raw is null) throw new ArgumentNullException(nameof(raw));
        var s = raw.Trim();
        if (s.Length == 0) throw new ArgumentException("path is empty after trim.", nameof(raw));
        s = s.Replace('\\', '/');
        // collapse runs of '/' but preserve leading '/' and any UNC-like double prefix at index 0.
        var sb = new System.Text.StringBuilder(s.Length);
        char? prev = null;
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '/' && prev == '/' && i != 1) continue;
            sb.Append(c);
            prev = c;
        }
        s = sb.ToString();
        // strip trailing slash unless that would empty the string
        while (s.Length > 1 && s.EndsWith("/", StringComparison.Ordinal)) s = s[..^1];
        return s;
    }

    /// <summary>
    /// Resolve <paramref name="canonicalPath"/> against
    /// <paramref name="sourceRootAbs"/> (an absolute, normalised path that
    /// names the configured source directory) and return the source-relative
    /// canonical path. Returns null when the resolved location escapes
    /// the source root — the caller maps that to exit 12.
    /// </summary>
    /// <remarks>
    /// Accepts:
    ///   * relative paths interpreted under the source root
    ///   * absolute paths that, after normalisation, lie under the source root
    /// Rejects:
    ///   * relative paths whose '..' segments climb above the source root
    ///   * absolute paths whose normalised form is not under the source root
    /// </remarks>
    public static string? ResolveUnderSource(string canonicalPath, string sourceRootAbs)
    {
        if (string.IsNullOrEmpty(canonicalPath)) return null;

        // The source-root path passed in MUST already be absolute and normalised.
        // We compare canonical forms with a trailing separator to make sure
        // "/foo/barbaz" is not classified as under "/foo/bar".
        var rootAbsNormalised = Path.GetFullPath(sourceRootAbs).Replace('\\', '/');
        if (rootAbsNormalised.EndsWith("/", StringComparison.Ordinal))
            rootAbsNormalised = rootAbsNormalised[..^1];

        string resolvedAbs;
        try
        {
            // Path.IsPathRooted treats "/foo" as rooted on POSIX; on Windows
            // a leading "/" is rooted to the current drive but Path.GetFullPath
            // normalises it. Either way, GetFullPath rooted at sourceRoot will
            // produce the right behaviour for relative inputs.
            resolvedAbs = Path.IsPathRooted(canonicalPath)
                ? Path.GetFullPath(canonicalPath)
                : Path.GetFullPath(Path.Combine(rootAbsNormalised, canonicalPath));
        }
        catch
        {
            return null;
        }
        var resolvedNormalised = resolvedAbs.Replace('\\', '/');
        if (resolvedNormalised.EndsWith("/", StringComparison.Ordinal))
            resolvedNormalised = resolvedNormalised[..^1];

        // Same path is allowed (excluding the entire source root)
        if (string.Equals(resolvedNormalised, rootAbsNormalised, StringComparison.OrdinalIgnoreCase))
            return ".";

        var prefix = rootAbsNormalised + "/";
        if (!resolvedNormalised.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return resolvedNormalised[prefix.Length..];
    }

    /// <summary>
    /// Returns true iff the path refers to a regular file on disk, OR
    /// the path does not exist on disk and ends with a recognised
    /// file-suffix (research R2). Maps to exit 16 in the runner.
    /// </summary>
    public static bool LooksLikeFilePath(string canonicalPath, string sourceRootAbs, string sourceRelative)
    {
        // Use the absolute path resolved by ResolveUnderSource so that
        // absolute-input vs relative-input behave identically.
        string abs;
        try
        {
            abs = sourceRelative == "."
                ? Path.GetFullPath(sourceRootAbs)
                : Path.GetFullPath(Path.Combine(sourceRootAbs, sourceRelative));
        }
        catch
        {
            return false;
        }

        if (File.Exists(abs)) return true;
        if (Directory.Exists(abs)) return false;

        // Path doesn't exist. Check suffix heuristic.
        foreach (var suf in LikelyFileSuffixes)
        {
            if (canonicalPath.EndsWith(suf, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// Classification of one source-relative path against the (existing ∪
    /// already-accepted-this-batch) exclusion set.
    /// </summary>
    public enum Classification
    {
        Added,
        RedundantSelf,
        RedundantUnderAncestor,
    }

    /// <summary>
    /// Classify <paramref name="path"/> against <paramref name="known"/> (the
    /// merged set of pre-existing exclusions plus paths already accepted
    /// earlier in this batch). All paths in <paramref name="known"/> MUST
    /// already be canonical source-relative forward-slash strings.
    /// </summary>
    public static (Classification Cls, string? Ancestor) ClassifyAgainstExisting(
        string path, IReadOnlyCollection<string> known)
    {
        foreach (var k in known)
        {
            if (string.Equals(path, k, StringComparison.OrdinalIgnoreCase))
                return (Classification.RedundantSelf, k);
            if (IsUnder(path, k))
                return (Classification.RedundantUnderAncestor, k);
        }
        return (Classification.Added, null);
    }

    /// <summary>
    /// Source-relative directory-boundary "is X under Y?" check. Y is the
    /// candidate ancestor; X is the candidate descendant. Same path returns
    /// false (use <see cref="Classification.RedundantSelf"/> for that case).
    /// </summary>
    public static bool IsUnder(string descendant, string ancestor)
    {
        if (descendant.Length <= ancestor.Length) return false;
        if (!descendant.StartsWith(ancestor, StringComparison.OrdinalIgnoreCase)) return false;
        return descendant[ancestor.Length] == '/';
    }

    /// <summary>
    /// Sort the supplied canonical source-relative paths ancestor-first
    /// (lexicographic), de-dupe self-equal paths, and classify each path
    /// against the running set. Returns one record per supplied path in
    /// input order, plus the deduplicated "to insert" list in lexicographic
    /// order. Research R1.
    /// </summary>
    public static BatchClassification ClassifyBatch(
        IReadOnlyList<string> sourceRelativePaths,
        IReadOnlyCollection<string> existing)
    {
        var perInput = new List<(string Path, Classification Cls, string? Ancestor)>(sourceRelativePaths.Count);
        var working = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        // Sort distinct paths ancestor-first lexicographically and walk forward.
        var sortedDistinct = sourceRelativePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var addedInOrder = new List<string>();
        var sortedClass = new Dictionary<string, (Classification Cls, string? Ancestor)>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in sortedDistinct)
        {
            var (cls, anc) = ClassifyAgainstExisting(p, working);
            sortedClass[p] = (cls, anc);
            if (cls == Classification.Added)
            {
                addedInOrder.Add(p);
                working.Add(p);
            }
        }

        // Build per-input view (preserve original supply order, including duplicates).
        var seenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in sourceRelativePaths)
        {
            if (!seenInBatch.Add(p))
            {
                perInput.Add((p, Classification.RedundantSelf, p));
                continue;
            }
            perInput.Add((p, sortedClass[p].Cls, sortedClass[p].Ancestor));
        }

        return new BatchClassification(perInput, addedInOrder);
    }

    /// <summary>Result of <see cref="ClassifyBatch"/>.</summary>
    public sealed record BatchClassification(
        IReadOnlyList<(string Path, Classification Cls, string? Ancestor)> PerInput,
        IReadOnlyList<string> AddedAscending);
}
