using System.Collections.Generic;
using System.IO;
using System.Linq;
using D2Net.Init;
using D2Net.Scaffold.Models;
using Npgsql;

namespace D2Net.Scaffold;

/// <summary>
/// Pure (no filesystem mutation) planner that walks the live source tree,
/// reads the existing <c>scaffold_tracker</c> rows, and produces a
/// <see cref="ScaffoldPlan"/> describing every file the runner will need to
/// copy and every <c>__&lt;basename&gt;/</c> workdir to create. Also
/// computes:
/// <list type="bullet">
///   <item>FR-010 / FR-011 reconciliation: <c>AddPaths</c> = live source minus tracker;
///         <c>RemovePaths</c> = tracker minus live source.</item>
///   <item>FR-013 collisions: per research R4, pre-walk scan that catches
///         <c>foo.dart</c> next to a non-dart entry whose name is
///         <c>__foo</c>.</item>
///   <item>FR-012 "is target ours?": true iff at least one tracker row
///         references a <c>target_parent_dir</c> under the configured
///         target root, OR the target directory itself does not exist.</item>
/// </list>
/// </summary>
public static class TargetTreePlanner
{
    public sealed record TrackerRow(string SourcePath, bool IsDart, string TargetParentDir);

    public static ScaffoldPlan Plan(
        string repoRoot,
        string sourceDir,
        string targetDir,
        IReadOnlyList<string> excludedPaths,
        NpgsqlConnection conn)
    {
        var sourceRootAbs = Path.GetFullPath(Path.Combine(repoRoot, sourceDir));
        var targetRootAbs = Path.GetFullPath(Path.Combine(repoRoot, targetDir));

        var liveSource = WalkLiveSource(sourceRootAbs, excludedPaths);
        var tracker = ReadTrackerRows(conn);

        var dartFiles = new List<DartFileTarget>();
        var nonDartFiles = new List<FileTarget>();

        // dart_files.full_path is forward-slash repo-rooted, so the source path
        // we record in scaffold_tracker has the same shape.
        var repoRootAbsNorm = Path.GetFullPath(repoRoot);
        foreach (var (absSourcePath, isDart) in liveSource)
        {
            var relRepo = RelPath.Normalize(Path.GetRelativePath(repoRootAbsNorm, absSourcePath));
            // Compute target absolute path: <targetRootAbs>/<rel-source-under-source-root> in native form.
            var relUnderSource = Path.GetRelativePath(sourceRootAbs, absSourcePath);
            var absTargetPath = Path.GetFullPath(Path.Combine(targetRootAbs, relUnderSource));
            var targetParentDir = Path.GetDirectoryName(absTargetPath) ?? targetRootAbs;

            if (isDart)
            {
                var basename = Path.GetFileNameWithoutExtension(absSourcePath);
                var workdirName = "__" + basename;
                var workdirAbsPath = Path.Combine(targetParentDir, workdirName);
                dartFiles.Add(new DartFileTarget(
                    RelPath: relRepo,
                    AbsSourcePath: absSourcePath,
                    AbsTargetPath: absTargetPath,
                    TargetParentDirAbsNative: targetParentDir,
                    WorkdirName: workdirName,
                    WorkdirAbsPath: workdirAbsPath));
            }
            else
            {
                nonDartFiles.Add(new FileTarget(
                    RelPath: relRepo,
                    AbsSourcePath: absSourcePath,
                    AbsTargetPath: absTargetPath,
                    TargetParentDirAbsNative: targetParentDir));
            }
        }

        // FR-013 / R4: pre-walk collision detection.
        var collisions = DetectCollisions(dartFiles, sourceRootAbs);

        // FR-010 / FR-011 reconciliation sets.
        var liveSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in dartFiles) liveSet.Add(f.RelPath);
        foreach (var f in nonDartFiles) liveSet.Add(f.RelPath);

        var trackerSet = new HashSet<string>(tracker.Keys, StringComparer.OrdinalIgnoreCase);

        var addPaths = liveSet
            .Except(trackerSet, StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var removePaths = trackerSet
            .Except(liveSet, StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // FR-012 "is target ours?": true iff (a) the live target dir does not
        // exist, OR (b) any tracker row references a target_parent_dir that
        // sits under the configured target root.
        var targetExists = Directory.Exists(targetRootAbs)
                           || File.Exists(targetRootAbs);
        bool targetIsManaged;
        if (!targetExists)
        {
            targetIsManaged = true; // nothing in the way; the run will create it.
        }
        else if (tracker.Count == 0)
        {
            targetIsManaged = false; // exists, but no tracker rows -> not ours.
        }
        else
        {
            // At least one tracker row whose target_parent_dir lies under the configured target root.
            var prefix = targetRootAbs.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            targetIsManaged = tracker.Values.Any(row =>
                string.Equals(
                    row.TargetParentDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    targetRootAbs.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    System.StringComparison.OrdinalIgnoreCase)
                || row.TargetParentDir.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase));
        }

        return new ScaffoldPlan(
            DartFiles: dartFiles,
            NonDartFiles: nonDartFiles,
            AddPaths: addPaths,
            RemovePaths: removePaths,
            Collisions: collisions,
            TargetIsManaged: targetIsManaged,
            TargetExists: targetExists);
    }

    /// <summary>
    /// Walk the source tree, skipping every excluded subtree (FR-004).
    /// Returns each file's absolute path and a flag for <c>.dart</c>.
    /// </summary>
    private static IReadOnlyList<(string AbsPath, bool IsDart)> WalkLiveSource(
        string sourceRootAbs,
        IReadOnlyList<string> excludedRelPaths)
    {
        var results = new List<(string, bool)>();
        if (!Directory.Exists(sourceRootAbs)) return results;

        var excludedAbs = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var rel in excludedRelPaths)
        {
            if (string.IsNullOrWhiteSpace(rel)) continue;
            var native = rel.Replace('/', Path.DirectorySeparatorChar);
            excludedAbs.Add(Path.GetFullPath(Path.Combine(sourceRootAbs, native)));
        }

        WalkRec(sourceRootAbs, excludedAbs, results);
        results.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
        return results;
    }

    private static void WalkRec(string dir, HashSet<string> excludedAbs, List<(string, bool)> results)
    {
        foreach (var f in Directory.EnumerateFiles(dir))
        {
            var isDart = Path.GetExtension(f).Equals(".dart", System.StringComparison.OrdinalIgnoreCase);
            results.Add((f, isDart));
        }
        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            var subAbs = Path.GetFullPath(sub);
            if (excludedAbs.Contains(subAbs)) continue;
            WalkRec(sub, excludedAbs, results);
        }
    }

    private static IReadOnlyList<ScaffoldCollision> DetectCollisions(
        IReadOnlyList<DartFileTarget> dartFiles,
        string sourceRootAbs)
    {
        var collisions = new List<ScaffoldCollision>();
        foreach (var dart in dartFiles)
        {
            var dartParentNative = Path.GetDirectoryName(dart.AbsSourcePath);
            if (string.IsNullOrEmpty(dartParentNative)) continue;
            var sourceWorkdirAbs = Path.Combine(dartParentNative, dart.WorkdirName);

            // Conflict if the same name already exists in the SOURCE tree as either:
            //   * a non-empty directory, or
            //   * a real file.
            // An empty source directory with that name is treated as benign — scaffold owns the workdir.
            if (File.Exists(sourceWorkdirAbs))
            {
                collisions.Add(new ScaffoldCollision(
                    DartFileRelPath: dart.RelPath,
                    ConflictingRelPath: RelPathRelative(sourceRootAbs, sourceWorkdirAbs),
                    Detail: $"a real file at {sourceWorkdirAbs} would be overwritten by the working directory"));
            }
            else if (Directory.Exists(sourceWorkdirAbs))
            {
                bool nonEmpty = Directory.EnumerateFileSystemEntries(sourceWorkdirAbs).Any();
                if (nonEmpty)
                {
                    collisions.Add(new ScaffoldCollision(
                        DartFileRelPath: dart.RelPath,
                        ConflictingRelPath: RelPathRelative(sourceRootAbs, sourceWorkdirAbs),
                        Detail: $"a non-empty directory at {sourceWorkdirAbs} would be overwritten by the working directory"));
                }
            }
        }
        return collisions;
    }

    private static string RelPathRelative(string root, string abs)
    {
        var rel = Path.GetRelativePath(root, abs);
        return rel.Replace('\\', '/');
    }

    private static IDictionary<string, TrackerRow> ReadTrackerRows(NpgsqlConnection conn)
    {
        var rows = new Dictionary<string, TrackerRow>(System.StringComparer.OrdinalIgnoreCase);
        // Defensive: scaffold_tracker may not exist yet on a freshly-init'd workspace.
        // Test-and-skip: SELECT to_regclass('public.scaffold_tracker') is NULL on absence.
        bool exists;
        using (var probe = conn.CreateCommand())
        {
            probe.CommandText = "SELECT to_regclass('public.scaffold_tracker') IS NOT NULL;";
            exists = (bool)(probe.ExecuteScalar() ?? false);
        }
        if (!exists) return rows;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT source_path, is_dart, target_parent_dir FROM scaffold_tracker;";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            var sp = rdr.GetString(0);
            rows[sp] = new TrackerRow(sp, rdr.GetBoolean(1), rdr.GetString(2));
        }
        return rows;
    }
}
