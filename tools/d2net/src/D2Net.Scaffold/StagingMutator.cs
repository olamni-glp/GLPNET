using System.IO;

namespace D2Net.Scaffold;

/// <summary>
/// Writes the entire scaffold output into a sibling staging directory
/// (<c>&lt;target&gt;.d2net-tmp/</c>). On success the runner atomically
/// renames the staging directory over the live target after the database
/// transaction commits (FR-014). Throws <see cref="IOException"/> on any
/// IO failure; the caller is responsible for deleting the staging tree
/// in <c>finally</c>.
/// </summary>
public static class StagingMutator
{
    /// <summary>Empty file the scaffold writes inside the target tree as a
    /// non-semantic operator-visible hint per spec clarification 2026-05-01.</summary>
    public const string SentinelFileName = ".d2net-scaffold-tracker";

    /// <summary>Sibling-of-target staging suffix (FR-014).</summary>
    public const string StagingSuffix = ".d2net-tmp";

    /// <summary>Side-aside suffix used during the atomic-rename of an
    /// existing live target (3-step rename on Windows).</summary>
    public const string OldSuffix = ".d2net-old";

    public static string ComputeStagingDir(string targetRootAbs)
    {
        var trimmed = targetRootAbs.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed + StagingSuffix;
    }

    public static string ComputeOldDir(string targetRootAbs)
    {
        var trimmed = targetRootAbs.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return trimmed + OldSuffix;
    }

    /// <summary>
    /// Result of <see cref="WriteStaging"/>: counts the runner reports in the success summary.
    /// </summary>
    public sealed record StagingCounts(int FilesCopied, int WorkdirsCreated);

    /// <summary>
    /// Wipe any leftover staging dir, recreate it, then copy every planned
    /// non-dart and dart file into it. For each dart file, also create the
    /// empty <c>__&lt;basename&gt;/</c> sibling working directory. Writes
    /// the empty sentinel file at the staging-tree root.
    /// </summary>
    public static StagingCounts WriteStaging(ScaffoldPlan plan, string stagingDir)
    {
        if (Directory.Exists(stagingDir))
            Directory.Delete(stagingDir, recursive: true);
        Directory.CreateDirectory(stagingDir);

        int filesCopied = 0;
        int workdirsCreated = 0;

        foreach (var f in plan.NonDartFiles)
        {
            var rel = Path.GetRelativePath(GetTargetRoot(f.AbsTargetPath, stagingDir), f.AbsTargetPath);
            var stagingFilePath = Path.Combine(stagingDir, rel);
            EnsureParentDir(stagingFilePath);
            File.Copy(f.AbsSourcePath, stagingFilePath, overwrite: true);
            filesCopied++;
        }

        foreach (var d in plan.DartFiles)
        {
            var rel = Path.GetRelativePath(GetTargetRoot(d.AbsTargetPath, stagingDir), d.AbsTargetPath);
            var stagingFilePath = Path.Combine(stagingDir, rel);
            EnsureParentDir(stagingFilePath);
            File.Copy(d.AbsSourcePath, stagingFilePath, overwrite: true);
            filesCopied++;

            // Create the empty __<basename>/ workdir at the same staging-relative parent.
            var stagingParent = Path.GetDirectoryName(stagingFilePath)!;
            var stagingWorkdir = Path.Combine(stagingParent, d.WorkdirName);
            Directory.CreateDirectory(stagingWorkdir);
            workdirsCreated++;
        }

        // Sentinel file at the staging-tree root.
        File.WriteAllBytes(Path.Combine(stagingDir, SentinelFileName), System.Array.Empty<byte>());

        return new StagingCounts(filesCopied, workdirsCreated);
    }

    /// <summary>
    /// Atomic rename of the staging directory over the live target.
    /// On Windows <see cref="Directory.Move"/> does not overwrite, so when
    /// a live target exists this is a 3-step compensating sequence:
    ///   1. rename live target to <c>&lt;target&gt;.d2net-old/</c>
    ///   2. rename staging to live target
    ///   3. delete the .d2net-old subtree
    /// On any failure inside this window the staging directory may remain
    /// on disk for the operator to investigate; the runner reports
    /// <see cref="ExitCodes.ScaffoldCopyError"/> per FR-014.
    /// </summary>
    public static void AtomicRenameStagingToLive(string stagingDir, string liveTargetDir)
    {
        var oldDir = ComputeOldDir(liveTargetDir);

        // Defensive cleanup: if a previous failed run left .d2net-old behind, delete it now.
        if (Directory.Exists(oldDir))
        {
            try { Directory.Delete(oldDir, recursive: true); } catch { /* best effort */ }
        }

        if (Directory.Exists(liveTargetDir))
        {
            // Step 1: live -> old
            Directory.Move(liveTargetDir, oldDir);
            try
            {
                // Step 2: staging -> live
                Directory.Move(stagingDir, liveTargetDir);
            }
            catch
            {
                // Compensate: try to put the live target back.
                try
                {
                    if (!Directory.Exists(liveTargetDir) && Directory.Exists(oldDir))
                        Directory.Move(oldDir, liveTargetDir);
                }
                catch { /* best effort; rethrow original */ }
                throw;
            }

            // Step 3: cleanup .d2net-old.
            if (Directory.Exists(oldDir))
            {
                try { Directory.Delete(oldDir, recursive: true); } catch { /* best effort */ }
            }
        }
        else
        {
            // Fast path: live target does not exist; rename staging directly into place.
            Directory.Move(stagingDir, liveTargetDir);
        }
    }

    /// <summary>
    /// Recursively delete a target directory (used by FR-012a destructive
    /// override). On failure raises <see cref="IOException"/>; the runner
    /// maps to <see cref="ExitCodes.ScaffoldCopyError"/>.
    /// </summary>
    public static void DeleteTargetTree(string liveTargetDir)
    {
        if (!Directory.Exists(liveTargetDir)) return;
        Directory.Delete(liveTargetDir, recursive: true);
    }

    private static void EnsureParentDir(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Recover the target-root portion of a planned absolute target path
    /// so we can compute the staging-relative path. The plan computed
    /// <c>AbsTargetPath</c> as <c>&lt;targetRootAbs&gt;/&lt;rel&gt;</c>;
    /// the staging dir is <c>&lt;targetRootAbs&gt;.d2net-tmp</c>; therefore
    /// the rel path is the same in both layouts.
    /// </summary>
    private static string GetTargetRoot(string absTargetPath, string stagingDir)
    {
        // stagingDir is <target>.d2net-tmp; the target root is stagingDir minus that suffix.
        if (stagingDir.EndsWith(StagingSuffix, System.StringComparison.Ordinal))
            return stagingDir[..^StagingSuffix.Length];
        return Path.GetDirectoryName(absTargetPath)!;
    }
}
