using System.Diagnostics;
using D2Net.Scaffold.Models;

namespace D2Net.Scaffold;

public static class ScaffoldRunner
{
    public sealed record Outcome(int ExitCode, RunSummary? Summary, IReadOnlyList<Collision> Collisions, string? Error);

    public static Outcome Run(ScaffoldOptions options)
    {
        // Argument validation
        if (!Directory.Exists(options.SourceRoot))
            return new Outcome(2, null, Array.Empty<Collision>(), $"Source directory does not exist: {options.SourceRoot}");

        if (PathOverlaps(options.SourceRoot, options.TargetRoot))
            return new Outcome(4, null, Array.Empty<Collision>(),
                $"Target path is the same as, or nested inside, the source path. Source={options.SourceRoot} Target={options.TargetRoot}");

        if (Directory.Exists(options.TargetRoot))
            return new Outcome(3, null, Array.Empty<Collision>(),
                $"Target directory already exists: {options.TargetRoot}");

        var sw = Stopwatch.StartNew();

        // Pre-flight (FR-012, R6): build the work plan, detect collisions; abort before any write if any.
        var plan = WorkPlan.Build(options);
        var collisions = PreflightChecker.Detect(plan);
        if (collisions.Count > 0)
            return new Outcome(5, null, collisions, "Companion-file collisions detected; aborting before writing target.");

        var summary = new RunSummary
        {
            Mode = "fresh",
            SourceRoot = options.SourceRoot,
            TargetRoot = options.TargetRoot,
            TrackerPath = TrackerWriter.TrackerPathFor(options.TargetRoot),
        };

        // Create target root + directory hierarchy
        Directory.CreateDirectory(options.TargetRoot);
        foreach (var dir in plan.Directories)
        {
            var absTarget = Path.Combine(options.TargetRoot, RelPath.ToNative(dir.RelPath));
            Directory.CreateDirectory(absTarget);
            summary.DirectoriesCreated++;
        }

        // Copy non-Dart files verbatim (FR-003)
        foreach (var f in plan.NonDartFiles)
        {
            var absTarget = Path.Combine(options.TargetRoot, RelPath.ToNative(f.RelPath));
            FileCopier.CopyVerbatim(f.AbsSourcePath, absTarget);
            summary.NonDartFilesCopied++;
        }

        // Copy each .dart -> <name>.dart.src (FR-004) and write nine companion stubs (FR-005, FR-006)
        foreach (var f in plan.DartFiles)
        {
            var absTargetDartSrc = Path.Combine(options.TargetRoot, RelPath.ToNative(f.RelPath) + ".src");
            FileCopier.CopyAsDartSrc(f.AbsSourcePath, absTargetDartSrc);
            summary.DartSrcFilesWritten++;

            var nativeRel = RelPath.ToNative(f.RelPath);
            var targetDir = Path.GetDirectoryName(Path.Combine(options.TargetRoot, nativeRel))!;
            var dartBaseName = Path.GetFileNameWithoutExtension(nativeRel); // strips trailing .dart
            summary.CompanionStubsWritten += CompanionFileWriter.WriteAllNine(targetDir, dartBaseName);
        }

        // Tracker (FR-007 – FR-010)
        summary.TrackerRecordsWritten = TrackerWriter.WriteFreshTracker(options.TargetRoot, plan.DartFiles);

        sw.Stop();
        summary.Elapsed = sw.Elapsed;
        return new Outcome(0, summary, Array.Empty<Collision>(), null);
    }

    internal static bool PathOverlaps(string source, string target)
    {
        var src = Path.GetFullPath(source);
        var tgt = Path.GetFullPath(target);
        var srcSep = src.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var tgtSep = tgt.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (string.Equals(srcSep, tgtSep, StringComparison.OrdinalIgnoreCase))
            return true;
        if (tgtSep.StartsWith(srcSep, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
