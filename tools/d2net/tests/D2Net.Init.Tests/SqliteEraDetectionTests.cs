using System.IO;
using System.Linq;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

/// <summary>
/// US3 / FR-014 / Q5 clarification: a workspace from the shipped 002 (SQLite-backed)
/// implementation is detected and refused without --FORCE --DELETE-EXISTING. With
/// the override, the upgraded command rebuilds wholesale -- no automatic data
/// migration.
/// </summary>
public class SqliteEraDetectionTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd)
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(""), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    private static void StubSqliteEra(string repoRoot, bool withLegacyFile, bool withSqliteJson)
    {
        var workspace = Path.Combine(repoRoot, ".D2NET");
        var pgdir = Path.Combine(workspace, "pgdb");
        Directory.CreateDirectory(pgdir);
        if (withLegacyFile)
        {
            File.WriteAllBytes(Path.Combine(pgdir, "workspace.sqlite"), new byte[] { 0x53, 0x51, 0x4C });
        }
        if (withSqliteJson)
        {
            File.WriteAllText(Path.Combine(workspace, "D2NET-Settings.json"),
                """{"schema_version":1,"connection":{"engine":"sqlite","db_file":"workspace.sqlite"}}""");
        }
    }

    [Fact]
    public void DetectsSqliteEraByLegacyFile()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/x.dart");
        StubSqliteEra(repo.Root, withLegacyFile: true, withSqliteJson: false);

        var (code, _, stderr) = Run(
            new[] { "--source", "glp_runtime", "--target-extension", "_net",
                    "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                    "--non-interactive" },
            repo.Root);
        Assert.Equal(ExitCodes.WorkspaceAlreadyExists, code);
        Assert.Contains("workspace already exists", stderr);
        Assert.Contains("--FORCE --DELETE-EXISTING", stderr);
        Assert.True(File.Exists(Path.Combine(repo.Root, ".D2NET", "pgdb", "workspace.sqlite")),
            "legacy SQLite file must be preserved when refusing");
    }

    [Fact]
    public void DetectsSqliteEraByJsonEngineField()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/x.dart");
        StubSqliteEra(repo.Root, withLegacyFile: false, withSqliteJson: true);

        var (code, _, _) = Run(
            new[] { "--source", "glp_runtime", "--target-extension", "_net",
                    "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                    "--non-interactive" },
            repo.Root);
        Assert.Equal(ExitCodes.WorkspaceAlreadyExists, code);
    }

    [Fact]
    public void ForceDeleteRebuildsAsPgliteWorkspace()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/x.dart");
        StubSqliteEra(repo.Root, withLegacyFile: true, withSqliteJson: true);

        var port = PortPicker.NextFreePort();
        var (code, _, _) = Run(
            new[] { "--source", "glp_runtime", "--target-extension", "_net",
                    "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                    "--non-interactive", "--bridge-port", port.ToString(),
                    "--FORCE", "--DELETE-EXISTING" },
            repo.Root);
        Assert.Equal(ExitCodes.Success, code);

        // The legacy SQLite file MUST be gone.
        Assert.False(File.Exists(Path.Combine(repo.Root, ".D2NET", "pgdb", "workspace.sqlite")));

        // The pgdb directory is now a multi-file PGLite tree.
        var pgdb = Path.Combine(repo.Root, ".D2NET", "pgdb");
        Assert.True(Directory.EnumerateFiles(pgdb).Count() > 1);

        // Settings now records connection.engine = "pglite".
        var json = File.ReadAllText(Path.Combine(repo.Root, ".D2NET", "D2NET-Settings.json"));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("pglite", doc.RootElement.GetProperty("connection").GetProperty("engine").GetString());
    }
}
