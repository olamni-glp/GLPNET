using System.IO;
using System.Linq;
using System.Text.Json;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

public class FreshInitTests
{
    private static (int code, string stdout, string stderr) Run(
        string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    [Fact]
    public void HappyPath_CreatesWorkspaceAndPopulatesAllFiveTables()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runtime/runner.dart")
            .AddDartFile("lib/runtime/heap.dart")
            .AddDartFile("test/widget/sample.dart")
            .AddDartFile("archive_2024/old.dart")
            .AddDartFile("legacy_lib/legacy.dart");

        var (code, stdout, stderr) = Run(
            new[] { "--source", "glp_runtime", "--target-extension", "_net",
                    "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                    "--non-interactive" },
            repo.Root);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("workspace ready", stdout);

        var workspace = Path.Combine(repo.Root, ".D2NET");
        Assert.True(Directory.Exists(workspace), "workspace dir missing");
        Assert.True(File.Exists(Path.Combine(workspace, "D2NET-Settings.json")), "settings missing");
        var dbFile = Path.Combine(workspace, "pgdb", "workspace.sqlite");
        Assert.True(File.Exists(dbFile), "sqlite db missing");

        // SC-002: settings JSON parses and has required fields
        var json = File.ReadAllText(Path.Combine(workspace, "D2NET-Settings.json"));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("glp_runtime", doc.RootElement.GetProperty("source_dir").GetString());
        Assert.Equal("_net", doc.RootElement.GetProperty("target_extension").GetString());
        Assert.Equal("glp_runtime_net", doc.RootElement.GetProperty("target_dir").GetString());
        Assert.True(doc.RootElement.TryGetProperty("created_at", out _));
        var conn = doc.RootElement.GetProperty("connection");
        Assert.Equal("sqlite", conn.GetProperty("engine").GetString());
        Assert.NotNull(conn.GetProperty("connection_string").GetString());

        // SC-003 + SC-004 + SC-005 + SC-006: tables and row counts
        using var verifier = new DbVerifier(dbFile);
        var tables = verifier.GetTableNames();
        Assert.Contains("setting", tables);
        Assert.Contains("excluded_directories", tables);
        Assert.Contains("dart_files", tables);
        Assert.Contains("phase_sequence", tables);
        Assert.Contains("phase_status", tables);

        // SC-006: phase tables created empty
        Assert.Equal(0, verifier.CountRows("phase_sequence"));
        Assert.Equal(0, verifier.CountRows("phase_status"));

        // SC-004: dart_files row count = .dart files outside excluded dirs
        // (3 in lib/test, 0 in archive_2024 + legacy_lib which auto-exclude)
        var dartFiles = verifier.GetDartFiles();
        Assert.Equal(3, dartFiles.Count);
        Assert.All(dartFiles, r => Assert.DoesNotContain("archive", r.FullPath));
        Assert.All(dartFiles, r => Assert.DoesNotContain("legacy", r.FullPath));
        // forward slashes only
        Assert.All(dartFiles, r => Assert.DoesNotContain("\\", r.FullPath));

        // SC-005: excluded_directories rows
        var exc = verifier.GetExclusions();
        Assert.Contains(exc, e => e.Path == "archive_2024" && e.Kind == "pattern");
        Assert.Contains(exc, e => e.Path == "legacy_lib" && e.Kind == "pattern");

        // setting table mirrors JSON
        Assert.Equal("glp_runtime", verifier.GetSetting("source_dir"));
        Assert.Equal("_net", verifier.GetSetting("target_extension"));
        Assert.Equal("glp_runtime_net", verifier.GetSetting("target_dir"));
        Assert.Equal("sqlite", verifier.GetSetting("db_engine"));
        Assert.Equal(dbFile, verifier.GetSetting("db_file"));
    }

    [Fact]
    public void EmptySource_CreatesWorkspaceWithEmptyDartFiles()
    {
        using var repo = new TempRepoBuilder();
        var (code, _, _) = Run(
            new[] { "--source", "glp_runtime", "--target-extension", "_net",
                    "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                    "--non-interactive" },
            repo.Root);

        Assert.Equal(ExitCodes.Success, code);
        var dbFile = Path.Combine(repo.Root, ".D2NET", "pgdb", "workspace.sqlite");
        using var verifier = new DbVerifier(dbFile);
        Assert.Equal(0, verifier.CountRows("dart_files"));
    }

    [Fact]
    public void RerunWithoutForceReturnsExit3()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/x.dart");
        var args = new[] { "--source", "glp_runtime", "--target-extension", "_net",
                           "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                           "--non-interactive" };
        var first = Run(args, repo.Root);
        Assert.Equal(ExitCodes.Success, first.code);

        var second = Run(args, repo.Root);
        Assert.Equal(ExitCodes.WorkspaceAlreadyExists, second.code);
        Assert.Contains("workspace already exists", second.stderr);
    }

    [Fact]
    public void ForceDeleteExistingReplacesWorkspace()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/x.dart");
        var args = new[] { "--source", "glp_runtime", "--target-extension", "_net",
                           "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                           "--non-interactive" };
        var first = Run(args, repo.Root);
        Assert.Equal(ExitCodes.Success, first.code);
        var firstSettingsAge = File.GetLastWriteTimeUtc(
            Path.Combine(repo.Root, ".D2NET", "D2NET-Settings.json"));
        Thread.Sleep(1100); // ensure mtime difference

        var argsForce = args.Concat(new[] { "--FORCE", "--DELETE-EXISTING" }).ToArray();
        var second = Run(argsForce, repo.Root);
        Assert.Equal(ExitCodes.Success, second.code);
        var secondSettingsAge = File.GetLastWriteTimeUtc(
            Path.Combine(repo.Root, ".D2NET", "D2NET-Settings.json"));
        Assert.True(secondSettingsAge > firstSettingsAge,
            $"settings file should be newer after force-delete: first={firstSettingsAge:o} second={secondSettingsAge:o}");

        // SC-008: workspace shape parity
        var dbFile = Path.Combine(repo.Root, ".D2NET", "pgdb", "workspace.sqlite");
        using var v = new DbVerifier(dbFile);
        var tables = v.GetTableNames();
        Assert.Contains("setting", tables);
        Assert.Contains("excluded_directories", tables);
        Assert.Contains("dart_files", tables);
        Assert.Contains("phase_sequence", tables);
        Assert.Contains("phase_status", tables);
    }

    [Fact]
    public void SourceMissingReturnsExit4()
    {
        using var repo = new TempRepoBuilder();
        var (code, _, stderr) = Run(
            new[] { "--source", "ghost_dir", "--target-extension", "_net",
                    "--target", "ghost_net", "--accept-suggested-exclusions",
                    "--non-interactive" },
            repo.Root);
        Assert.Equal(ExitCodes.SourceDirMissing, code);
        Assert.Contains("does not exist", stderr);
    }
}
