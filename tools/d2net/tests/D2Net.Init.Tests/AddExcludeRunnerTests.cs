using System.IO;
using System.Linq;
using System.Text.Json;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

public class AddExcludeRunnerTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    /// <summary>
    /// Build a temp repo with five .dart files: three under lib/, two under
    /// extra/. archive_2024/ is auto-excluded by init so we deliberately
    /// avoid that name. The init step itself is what feeds dart_files.
    /// </summary>
    private static (TempRepoBuilder repo, int port) BuildAndInit(int? bridgePort = null)
    {
        var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart")
            .AddDartFile("lib/heap.dart")
            .AddDartFile("lib/foo.dart")
            .AddDartFile("extra/a.dart")
            .AddDartFile("extra/b.dart");

        var port = bridgePort ?? PortPicker.NextFreePort();
        var (code, _, se) = Run(
            new[] { "--source", "glp_runtime", "--target-extension", "_net",
                    "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                    "--non-interactive", "--bridge-port", port.ToString() },
            repo.Root);
        if (code != ExitCodes.Success)
        {
            repo.Dispose();
            throw new System.InvalidOperationException(
                $"init for AddExcludeRunnerTests setup failed: exit={code} stderr={se}");
        }
        return (repo, port);
    }

    [Fact]
    public void SinglePathAddExclude_RemovesMatchingDartFiles_ExitsZero()
    {
        var (repo, port) = BuildAndInit();
        using (repo)
        {
            // Excluding "extra" should remove 2 dart_files rows (extra/a.dart, extra/b.dart).
            var (code, so, _) = Run(
                new[] { "--add-exclude", "extra", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("added:      1", so);
            Assert.Contains("removed:    2 dart_files row(s)", so);
            Assert.Contains("extra: 2 row(s)", so);

            // FR-013: post-success inspection reflects new state.
            var (lcode, lso, _) = Run(
                new[] { "--list", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, lcode);
            Assert.DoesNotContain("extra/a.dart", lso);
            Assert.DoesNotContain("extra/b.dart", lso);
            Assert.Contains("lib/runner.dart", lso);

            var (xcode, xso, _) = Run(
                new[] { "--Exclusions", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, xcode);
            Assert.Contains("extra", xso);
        }
    }

    [Fact]
    public void SinglePathAddExclude_JsonShape()
    {
        var (repo, port) = BuildAndInit();
        using (repo)
        {
            var (code, so, _) = Run(
                new[] { "--add-exclude", "extra", "--json", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, code);

            using var doc = JsonDocument.Parse(so);
            Assert.Equal("applied", doc.RootElement.GetProperty("result").GetString());
            var added = doc.RootElement.GetProperty("added");
            Assert.Equal(1, added.GetArrayLength());
            Assert.Equal("extra", added[0].GetString());
            var removed = doc.RootElement.GetProperty("removed_rows");
            Assert.Equal(1, removed.GetArrayLength());
            Assert.Equal("extra", removed[0].GetProperty("exclusion").GetString());
            Assert.Equal(2, removed[0].GetProperty("rows").GetInt32());
            var totals = doc.RootElement.GetProperty("totals");
            Assert.Equal(1, totals.GetProperty("added").GetInt32());
            Assert.Equal(0, totals.GetProperty("redundant").GetInt32());
            Assert.Equal(2, totals.GetProperty("removed_rows").GetInt32());
        }
    }

    [Fact]
    public void IdempotentReRun_IsNoOp()
    {
        var (repo, port) = BuildAndInit();
        using (repo)
        {
            // First run: adds "extra".
            var (c1, _, _) = Run(
                new[] { "--add-exclude", "extra", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, c1);

            // Second run with same args: redundant; exit 0; nothing removed.
            var (c2, so2, _) = Run(
                new[] { "--add-exclude", "extra", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, c2);
            Assert.Contains("added:      0", so2);
            Assert.Contains("redundant:  1", so2);
            Assert.Contains("removed:    0 dart_files row(s)", so2);
            Assert.Contains("already excluded", so2);
        }
    }

    [Fact]
    public void NonexistentDirectoryPath_AcceptedAsForwardLooking()
    {
        var (repo, port) = BuildAndInit();
        using (repo)
        {
            // does_not_exist/ has no entries; should add to exclusion list, remove 0.
            var (code, so, _) = Run(
                new[] { "--add-exclude", "does_not_exist", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("added:      1", so);
            Assert.Contains("removed:    0 dart_files row(s)", so);
        }
    }

    [Fact]
    public void MultiplePaths_AppliedInSingleTransaction()
    {
        var (repo, port) = BuildAndInit();
        using (repo)
        {
            // Add three exclusions. extra/ contains 2 dart files; lib/ contains 3;
            // empty_dir/ doesn't exist (forward-looking).
            var (code, so, _) = Run(
                new[] { "--add-exclude", "extra",
                        "--add-exclude", "empty_dir",
                        "--add-exclude", "lib",
                        "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("added:      3", so);
            Assert.Contains("removed:    5 dart_files row(s)", so);
            Assert.Contains("extra: 2 row(s)", so);
            Assert.Contains("lib: 3 row(s)", so);
            Assert.Contains("empty_dir: 0 row(s)", so);
        }
    }
}
