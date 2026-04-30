using System.Diagnostics;
using D2Net.Scaffold.Models;

namespace D2Net.Scaffold;

public static class RefreshRunner
{
    public static ScaffoldRunner.Outcome Run(ScaffoldOptions options)
    {
        if (!Directory.Exists(options.SourceRoot))
            return new ScaffoldRunner.Outcome(2, null, Array.Empty<Collision>(),
                $"Source directory does not exist: {options.SourceRoot}");

        if (ScaffoldRunner.PathOverlaps(options.SourceRoot, options.TargetRoot))
            return new ScaffoldRunner.Outcome(4, null, Array.Empty<Collision>(),
                $"Target path is the same as, or nested inside, the source path. Source={options.SourceRoot} Target={options.TargetRoot}");

        if (!Directory.Exists(options.TargetRoot))
            return new ScaffoldRunner.Outcome(6, null, Array.Empty<Collision>(),
                $"--refresh specified but target directory does not exist: {options.TargetRoot}");

        var sw = Stopwatch.StartNew();

        var plan = WorkPlan.Build(options);
        var collisions = PreflightChecker.Detect(plan);
        if (collisions.Count > 0)
            return new ScaffoldRunner.Outcome(5, null, collisions,
                "Companion-file collisions detected; aborting before writing target.");

        var summary = new RunSummary
        {
            Mode = "refresh",
            SourceRoot = options.SourceRoot,
            TargetRoot = options.TargetRoot,
            TrackerPath = TrackerWriter.TrackerPathFor(options.TargetRoot),
        };

        // Mirror directory hierarchy: create newly-added dirs; do not delete absent dirs (FR-011 (a)).
        foreach (var dir in plan.Directories)
        {
            var absTarget = Path.Combine(options.TargetRoot, RelPath.ToNative(dir.RelPath));
            if (!Directory.Exists(absTarget))
            {
                Directory.CreateDirectory(absTarget);
                summary.DirectoriesCreated++;
            }
        }

        // Refresh non-Dart files byte-for-byte (FR-011 (b)).
        foreach (var f in plan.NonDartFiles)
        {
            var absTarget = Path.Combine(options.TargetRoot, RelPath.ToNative(f.RelPath));
            FileCopier.CopyVerbatim(f.AbsSourcePath, absTarget);
            summary.NonDartFilesCopied++;
        }

        // Refresh .dart.src files byte-for-byte (FR-011 (c)); preserve existing companions (FR-011 (d));
        // create stubs for newly-discovered Dart files (FR-011 (e)).
        foreach (var f in plan.DartFiles)
        {
            var absTargetDartSrc = Path.Combine(options.TargetRoot, RelPath.ToNative(f.RelPath) + ".src");
            FileCopier.CopyAsDartSrc(f.AbsSourcePath, absTargetDartSrc);
            summary.DartSrcFilesWritten++;

            var nativeRel = RelPath.ToNative(f.RelPath);
            var targetDir = Path.GetDirectoryName(Path.Combine(options.TargetRoot, nativeRel))!;
            var dartBaseName = Path.GetFileNameWithoutExtension(nativeRel);
            var (written, wasNew) = CompanionFileWriter.WriteIfMissing(targetDir, dartBaseName);
            summary.CompanionStubsWritten += written;
            if (wasNew)
                summary.NewlyDiscoveredDartFiles.Add(f.RelPath);
        }

        // FR-011 (f): tracker is NOT modified by refresh.
        summary.TrackerRecordsWritten = 0;
        summary.TrackerPath = "";

        sw.Stop();
        summary.Elapsed = sw.Elapsed;
        return new ScaffoldRunner.Outcome(0, summary, Array.Empty<Collision>(), null);
    }
}
