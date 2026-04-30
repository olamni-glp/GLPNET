using System.IO;
using System.Linq;
using System.Text.Json;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;
using Npgsql;

namespace D2Net.Init.Tests;

public class InspectorIntegrationTests
{
    private static (int code, string stdout, string stderr) Run(
        string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    private static TempRepoBuilder BuildAndInit()
    {
        var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runtime/runner.dart")
            .AddDartFile("lib/runtime/heap.dart")
            .AddDartFile("lib/foo/bar.dart")
            .AddDartFile("archive_2024/old.dart");

        var port = PortPicker.NextFreePort();
        var (code, _, _) = Run(
            new[] { "--source", "glp_runtime", "--target-extension", "_net",
                    "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                    "--non-interactive", "--bridge-port", port.ToString() },
            repo.Root);
        Assert.Equal(ExitCodes.Success, code);
        return repo;
    }

    [Fact]
    public void List_PlainTextSortedByFullPath()
    {
        using var repo = BuildAndInit();
        var (code, stdout, _) = Run(new[] { "--list" }, repo.Root);
        Assert.Equal(ExitCodes.Success, code);
        var lines = stdout.Trim().Split('\n').Select(l => l.Trim('\r')).ToArray();
        Assert.Equal(3, lines.Length);
        Assert.Contains("bar.dart\tglp_runtime/lib/foo/bar.dart", lines[0]);
        Assert.Contains("heap.dart\tglp_runtime/lib/runtime/heap.dart", lines[1]);
        Assert.Contains("runner.dart\tglp_runtime/lib/runtime/runner.dart", lines[2]);
    }

    [Fact]
    public void List_JsonShape()
    {
        using var repo = BuildAndInit();
        var (code, stdout, _) = Run(new[] { "--list", "--json" }, repo.Root);
        Assert.Equal(ExitCodes.Success, code);
        using var doc = JsonDocument.Parse(stdout);
        var arr = doc.RootElement.GetProperty("dart_files");
        Assert.Equal(3, arr.GetArrayLength());
        var first = arr[0];
        Assert.True(first.GetProperty("id").GetInt64() > 0);
        Assert.NotNull(first.GetProperty("filename").GetString());
        Assert.NotNull(first.GetProperty("full_path").GetString());
    }

    [Fact]
    public void Exclusions_PlainText()
    {
        using var repo = BuildAndInit();
        var (code, stdout, _) = Run(new[] { "--Exclusions" }, repo.Root);
        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("archive_2024", stdout);
    }

    [Fact]
    public void Exclusions_JsonShape()
    {
        using var repo = BuildAndInit();
        var (code, stdout, _) = Run(new[] { "--Exclusions", "--json" }, repo.Root);
        Assert.Equal(ExitCodes.Success, code);
        using var doc = JsonDocument.Parse(stdout);
        var arr = doc.RootElement.GetProperty("excluded_directories");
        Assert.True(arr.GetArrayLength() >= 1);
    }

    [Fact]
    public void CurrentPhase_NoActivePhase_PlainText()
    {
        using var repo = BuildAndInit();
        var (code, stdout, _) = Run(new[] { "--current-phase" }, repo.Root);
        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("no active phase", stdout);
    }

    [Fact]
    public void CurrentPhase_NoActivePhase_Json()
    {
        using var repo = BuildAndInit();
        var (code, stdout, _) = Run(new[] { "--current-phase", "--json" }, repo.Root);
        Assert.Equal(ExitCodes.Success, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("phase").ValueKind);
    }

    [Fact]
    public void CurrentPhase_ReturnsLowestSequenceNonCompleted()
    {
        using var repo = BuildAndInit();
        var pgdb = Path.Combine(repo.Root, ".D2NET", "pgdb");

        // Inject test rows via a separate verifier-spawned bridge.
        using (var v = new DbVerifier(pgdb))
        {
            using var cmd = v.RawConnection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO phase_sequence(phase, sequence) VALUES
                    ('init', 1), ('scaffold', 2), ('analyze', 3), ('port', 4);
                INSERT INTO phase_status(phase, status, last_updated) VALUES
                    ('init', 'COMPLETED', '2026-04-29T10:00:00Z'),
                    ('scaffold', 'COMPLETED', '2026-04-30T10:00:00Z'),
                    ('analyze', 'IN_PROGRESS', '2026-04-30T11:00:00Z'),
                    ('port', 'NOT_STARTED', '2026-04-30T11:30:00Z');
            ";
            cmd.ExecuteNonQuery();
        }

        var (code, stdout, _) = Run(new[] { "--current-phase" }, repo.Root);
        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("analyze", stdout);
        Assert.Contains("IN_PROGRESS", stdout);
        // FR-019 wire format preserved: ISO-8601 UTC with trailing 'Z'.
        Assert.Contains("2026-04-30T11:00:00Z", stdout);

        var (codeJ, stdoutJ, _) = Run(new[] { "--current-phase", "--json" }, repo.Root);
        Assert.Equal(ExitCodes.Success, codeJ);
        using var doc = JsonDocument.Parse(stdoutJ);
        Assert.Equal("analyze", doc.RootElement.GetProperty("phase").GetString());
        Assert.Equal("IN_PROGRESS", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("sequence").GetInt32());
        Assert.Equal("2026-04-30T11:00:00Z", doc.RootElement.GetProperty("last_updated").GetString());
    }

    [Fact]
    public void Inspection_DoesNotModifyUserVisibleState()
    {
        // 002 FR-017/FR-018/FR-019 ("makes no modification to the workspace") preserved by
        // 005 FR-013, interpreted as: USER-VISIBLE state (settings JSON, the five tables'
        // content) is unchanged. PGLite's intrinsic startup activity (pg_xact, pg_subtrans,
        // WAL segment extension) is bookkeeping under `pgdb/` and not part of the user
        // contract. The smoke-seed removal in `bridge-direct.mjs` (analysis finding C1 +
        // contracts/pgbridge-contract.md "Smoke-test seed data") is what keeps even THAT
        // bookkeeping minimal across spawns.
        using var repo = BuildAndInit();
        var workspace = Path.Combine(repo.Root, ".D2NET");
        var settingsFile = Path.Combine(workspace, "D2NET-Settings.json");
        var pgdb = Path.Combine(workspace, "pgdb");

        var settingsBefore = File.ReadAllBytes(settingsFile);

        // Capture the five user-visible tables' content via a verifier.
        (int Setting, int Excl, int Dart, int PhaseSeq, int PhaseStat) countsBefore;
        using (var v = new DbVerifier(pgdb))
        {
            countsBefore = (
                v.CountRows("setting"),
                v.CountRows("excluded_directories"),
                v.CountRows("dart_files"),
                v.CountRows("phase_sequence"),
                v.CountRows("phase_status"));
        }

        Run(new[] { "--list" }, repo.Root);
        Run(new[] { "--Exclusions" }, repo.Root);
        Run(new[] { "--current-phase" }, repo.Root);
        Run(new[] { "--list", "--json" }, repo.Root);

        // Settings JSON is byte-identical.
        var settingsAfter = File.ReadAllBytes(settingsFile);
        Assert.Equal(settingsBefore, settingsAfter);

        // The five tables' row counts unchanged.
        using (var v = new DbVerifier(pgdb))
        {
            Assert.Equal(countsBefore.Setting,    v.CountRows("setting"));
            Assert.Equal(countsBefore.Excl,       v.CountRows("excluded_directories"));
            Assert.Equal(countsBefore.Dart,       v.CountRows("dart_files"));
            Assert.Equal(countsBefore.PhaseSeq,   v.CountRows("phase_sequence"));
            Assert.Equal(countsBefore.PhaseStat,  v.CountRows("phase_status"));
        }
    }
}
