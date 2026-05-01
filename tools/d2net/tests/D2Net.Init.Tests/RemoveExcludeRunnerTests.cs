using System.IO;
using System.Linq;
using System.Text.Json;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

public class RemoveExcludeRunnerTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    /// <summary>
    /// Build a temp repo. lib/ contains 3 .dart; extra/ contains 2 .dart.
    /// Init excludes nothing manual; we then add-exclude `extra` (manual)
    /// so we have a manual exclusion to remove.
    /// </summary>
    private static (TempRepoBuilder repo, int port) BuildAndInit_AddExtraExclusion(int? bridgePort = null)
    {
        var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart")
            .AddDartFile("lib/heap.dart")
            .AddDartFile("lib/foo.dart")
            .AddDartFile("extra/a.dart")
            .AddDartFile("extra/b.dart");

        var port = bridgePort ?? PortPicker.NextFreePort();
        var (icode, _, ise) = Run(
            new[] { "--source", "glp_runtime", "--target-extension", "_net",
                    "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                    "--non-interactive", "--bridge-port", port.ToString() },
            repo.Root);
        if (icode != ExitCodes.Success)
        {
            repo.Dispose();
            throw new System.InvalidOperationException(
                $"init for RemoveExcludeRunnerTests setup failed: exit={icode} stderr={ise}");
        }

        // Add `extra` as a manual exclusion to give us something to remove.
        var (acode, _, ase) = Run(
            new[] { "--add-exclude", "extra", "--bridge-port", port.ToString() },
            repo.Root);
        if (acode != ExitCodes.Success)
        {
            repo.Dispose();
            throw new System.InvalidOperationException(
                $"add-exclude for RemoveExcludeRunnerTests setup failed: exit={acode} stderr={ase}");
        }
        return (repo, port);
    }

    [Fact]
    public void SinglePathRemoveExclude_ReindexesDartFiles_ExitsZero()
    {
        var (repo, port) = BuildAndInit_AddExtraExclusion();
        using (repo)
        {
            // Pre-condition: 3 lib dart_files (extra was excluded so 2 were removed).
            var (lcode0, lso0, _) = Run(
                new[] { "--list", "--bridge-port", port.ToString() }, repo.Root);
            Assert.Equal(ExitCodes.Success, lcode0);
            int linesBefore = lso0.Split('\n').Count(l => l.Contains(".dart"));

            // Remove `extra`.
            var (code, so, _) = Run(
                new[] { "--remove-exclude", "extra", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("removed:                1", so);
            Assert.Contains("inserted:               2 dart_files row(s)", so);
            Assert.Contains("extra: 2 row(s)", so);

            // Post-success inspections reflect new state (FR-013).
            var (lcode, lso, _) = Run(
                new[] { "--list", "--bridge-port", port.ToString() }, repo.Root);
            Assert.Equal(ExitCodes.Success, lcode);
            Assert.Contains("extra/a.dart", lso);
            Assert.Contains("extra/b.dart", lso);

            var (xcode, xso, _) = Run(
                new[] { "--Exclusions", "--bridge-port", port.ToString() }, repo.Root);
            Assert.Equal(ExitCodes.Success, xcode);
            Assert.DoesNotContain("\nextra\n", "\n" + xso);
        }
    }

    [Fact]
    public void SinglePathRemoveExclude_JsonShape()
    {
        var (repo, port) = BuildAndInit_AddExtraExclusion();
        using (repo)
        {
            var (code, so, _) = Run(
                new[] { "--remove-exclude", "extra", "--json", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, code);

            using var doc = JsonDocument.Parse(so);
            Assert.Equal("applied", doc.RootElement.GetProperty("result").GetString());
            var removed = doc.RootElement.GetProperty("removed");
            Assert.Equal(1, removed.GetArrayLength());
            Assert.Equal("extra", removed[0].GetProperty("path").GetString());
            Assert.Equal("manual", removed[0].GetProperty("kind").GetString());
            var inserted = doc.RootElement.GetProperty("inserted_rows");
            Assert.Equal(1, inserted.GetArrayLength());
            Assert.Equal(2, inserted[0].GetProperty("rows").GetInt32());
            var totals = doc.RootElement.GetProperty("totals");
            Assert.Equal(1, totals.GetProperty("removed").GetInt32());
            Assert.Equal(0, totals.GetProperty("not_present").GetInt32());
            Assert.Equal(0, totals.GetProperty("covered_by_ancestor").GetInt32());
            Assert.Equal(2, totals.GetProperty("inserted_rows").GetInt32());
        }
    }

    [Fact]
    public void NotCurrentlyExcluded_NoOp_ExitsZero()
    {
        var (repo, port) = BuildAndInit_AddExtraExclusion();
        using (repo)
        {
            // `something_else` was never excluded.
            var (code, so, _) = Run(
                new[] { "--remove-exclude", "something_else", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("removed:                0", so);
            Assert.Contains("not-currently-excluded: 1", so);
            Assert.Contains("inserted:               0 dart_files row(s)", so);
        }
    }

    [Fact]
    public void IdempotentReRun_IsNoOp()
    {
        var (repo, port) = BuildAndInit_AddExtraExclusion();
        using (repo)
        {
            // First run: removes `extra`.
            var (c1, _, _) = Run(
                new[] { "--remove-exclude", "extra", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, c1);

            // Second run: `extra` is no longer in the exclusion list.
            var (c2, so2, _) = Run(
                new[] { "--remove-exclude", "extra", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, c2);
            Assert.Contains("removed:                0", so2);
            Assert.Contains("not-currently-excluded: 1", so2);
            Assert.Contains("inserted:               0 dart_files row(s)", so2);
        }
    }
}
