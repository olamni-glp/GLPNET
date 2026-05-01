using System.IO;
using System.Linq;
using System.Text.Json;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

/// <summary>
/// T015: happy-path round trip. Exercises FR-001 .. FR-008, FR-010 (initial
/// no-op variant), FR-018, SC-002, SC-003, SC-004, SC-009.
/// </summary>
public class ScaffoldHappyPathTests
{
    private static (TempRepoBuilder repo, int port) BuildAndInit(int? bridgePort = null)
    {
        var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart", "// runner")
            .AddDartFile("lib/heap.dart", "// heap")
            .AddDartFile("lib/io.dart", "// io")
            .AddDartFile("extra/glp_repl.dart", "// repl")
            .AddSourceFile("lib/pubspec.yaml", "name: test\n")
            .AddSourceFile("extra/run.sh", "#!/bin/sh\n")
            .AddSourceFile("archive_2024/old.dart", "// archived dart that should not be copied")
            .AddSourceFile("archive_2024/junk.txt", "junk");

        var (code, _, se, port) = InitHelper.Init(repo.Root, bridgePort);
        if (code != D2Net.Init.ExitCodes.Success)
        {
            repo.Dispose();
            throw new System.InvalidOperationException(
                $"init for ScaffoldHappyPathTests setup failed: exit={code} stderr={se}");
        }
        return (repo, port);
    }

    [Fact]
    public void Scaffold_FreshRun_CreatesTargetTreeWithWorkdirsAndSentinel()
    {
        var (repo, port) = BuildAndInit();
        using (repo)
        {
            // Auto-detected exclusions (kind='pattern') should include 'archive_2024' since
            // the ExclusionDetector tags `archive_*` directories. Add it explicitly to be safe.
            // Actually: add it via --add-exclude to ensure it's present in the workspace exclusion list.
            var (addCode, _, _) = InitHelper.AddExclude(repo.Root, port, "archive_2024");
            Assert.Equal(D2Net.Init.ExitCodes.Success, addCode);

            var (code, so, _) = InitHelper.Scaffold(repo.Root, port);
            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("target tree scaffolded", so);
            Assert.Contains("source            : glp_runtime", so);
            Assert.Contains("target            : glp_runtime_net", so);
            Assert.Contains("extension         : _net", so);

            // (a) target tree exists
            var targetRoot = Path.Combine(repo.Root, "glp_runtime_net");
            Assert.True(Directory.Exists(targetRoot));

            // (b) non-excluded files copied byte-identical
            Assert.True(File.Exists(Path.Combine(targetRoot, "lib", "runner.dart")));
            Assert.True(File.Exists(Path.Combine(targetRoot, "lib", "heap.dart")));
            Assert.True(File.Exists(Path.Combine(targetRoot, "lib", "io.dart")));
            Assert.True(File.Exists(Path.Combine(targetRoot, "lib", "pubspec.yaml")));
            Assert.True(File.Exists(Path.Combine(targetRoot, "extra", "glp_repl.dart")));
            Assert.True(File.Exists(Path.Combine(targetRoot, "extra", "run.sh")));
            Assert.Equal("// runner", File.ReadAllText(Path.Combine(targetRoot, "lib", "runner.dart")));
            Assert.Equal("name: test\n", File.ReadAllText(Path.Combine(targetRoot, "lib", "pubspec.yaml")));

            // (c) archive_2024 absent
            Assert.False(Directory.Exists(Path.Combine(targetRoot, "archive_2024")));

            // (d) __<basename>/ workdirs
            Assert.True(Directory.Exists(Path.Combine(targetRoot, "lib", "__runner")));
            Assert.True(Directory.Exists(Path.Combine(targetRoot, "lib", "__heap")));
            Assert.True(Directory.Exists(Path.Combine(targetRoot, "lib", "__io")));
            Assert.True(Directory.Exists(Path.Combine(targetRoot, "extra", "__glp_repl")));
            // workdirs are EMPTY
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(targetRoot, "lib", "__runner")));

            // (e) sentinel file
            Assert.True(File.Exists(Path.Combine(targetRoot, ".d2net-scaffold-tracker")));
            Assert.Equal(0, new FileInfo(Path.Combine(targetRoot, ".d2net-scaffold-tracker")).Length);

            // (f) DB columns populated for every dart file (SC-004).
            var workspace = Path.Combine(repo.Root, ".D2NET");
            using (var verifier = new DbVerifier(Path.Combine(workspace, "pgdb")))
            {
                var rows = verifier.GetDartFilesWithScaffoldColumns();
                Assert.Equal(4, rows.Count);
                foreach (var (full, parent, workdir) in rows)
                {
                    Assert.NotNull(parent);
                    Assert.NotNull(workdir);
                    Assert.StartsWith("__", workdir!);
                    // target_parent_dir is native-separator absolute under glp_runtime_net.
                    Assert.Contains("glp_runtime_net", parent!);
                    Assert.Contains(Path.DirectorySeparatorChar.ToString(), parent);
                }

                // scaffold_tracker rows for every copied path.
                var tracker = verifier.GetTrackerRows();
                Assert.True(tracker.Count >= 6);
                Assert.Contains(tracker, t => t.SourcePath.EndsWith("lib/runner.dart") && t.IsDart);
                Assert.Contains(tracker, t => t.SourcePath.EndsWith("lib/pubspec.yaml") && !t.IsDart);
                // archive_2024 should not appear at all.
                Assert.DoesNotContain(tracker, t => t.SourcePath.Contains("archive_2024"));

                // Phase rows: only 'scaffold' touched.
                var ph = verifier.GetPhaseStatus();
                Assert.Single(ph);
                Assert.Equal("scaffold", ph[0].Phase);
                Assert.Equal("COMPLETED", ph[0].Status);

                var seq = verifier.GetPhaseSequence();
                Assert.Single(seq);
                Assert.Equal("scaffold", seq[0].Phase);
            }

            // (FR-018 / SC-009) d2net-init --list --json reflects new columns.
            var (lcode, lso, _) = InitHelper.ListJson(repo.Root, port);
            Assert.Equal(D2Net.Init.ExitCodes.Success, lcode);
            using var doc = JsonDocument.Parse(lso);
            // The shape may evolve; minimum: dart_files array exists and rows have full_path.
            var dartFiles = doc.RootElement.GetProperty("dart_files");
            Assert.True(dartFiles.GetArrayLength() >= 4);
        }
    }

    [Fact]
    public void Scaffold_JsonShape_MatchesContract()
    {
        var (repo, port) = BuildAndInit();
        using (repo)
        {
            InitHelper.AddExclude(repo.Root, port, "archive_2024");

            var (code, so, _) = InitHelper.Scaffold(repo.Root, port, json: true);
            Assert.Equal(ExitCodes.Success, code);
            using var doc = JsonDocument.Parse(so);
            var root = doc.RootElement;

            Assert.Equal("applied", root.GetProperty("result").GetString());
            Assert.Equal("glp_runtime", root.GetProperty("source").GetString());
            Assert.Equal("glp_runtime_net", root.GetProperty("target").GetString());
            Assert.Equal("_net", root.GetProperty("extension").GetString());
            Assert.False(root.GetProperty("destructive_override_used").GetBoolean());
            Assert.Contains("glp_runtime_net", root.GetProperty("target_abs").GetString());

            var totals = root.GetProperty("totals");
            Assert.True(totals.GetProperty("files_copied").GetInt32() > 0);
            Assert.Equal(4, totals.GetProperty("workdirs_created").GetInt32());
            Assert.Equal(4, totals.GetProperty("dart_files_updated").GetInt32());
            Assert.True(totals.GetProperty("added_paths").GetInt32() > 0);
            Assert.Equal(0, totals.GetProperty("removed_paths").GetInt32());
            Assert.True(totals.GetProperty("duration_seconds").GetDouble() >= 0.0);
        }
    }
}
