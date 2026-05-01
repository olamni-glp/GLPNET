using System.IO;
using System.Text.Json;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

/// <summary>
/// US3 / FR-004a: refuse non-manual exclusions by default; allow with
/// --allow-system-exclusions override.
/// </summary>
public class RemoveExcludeSystemKindTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    /// <summary>
    /// Build a repo where init excludes `archive_2024` (kind='pattern' via
    /// the archive marker) and `.dart_tool` (kind='tool' if it exists).
    /// We also add a manual exclusion for round-trip comparison.
    /// </summary>
    private static (TempRepoBuilder repo, int port) BuildAndInit_WithSystemExclusions(int? bridgePort = null)
    {
        var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart")
            .AddDartFile("archive_2024/old.dart")    // pattern marker -> kind='pattern'
            .AddDartFile("extra/a.dart");

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
                $"init for RemoveExcludeSystemKindTests setup failed: exit={icode} stderr={ise}");
        }

        // Add `extra` as a manual exclusion.
        var (acode, _, ase) = Run(
            new[] { "--add-exclude", "extra", "--bridge-port", port.ToString() },
            repo.Root);
        if (acode != ExitCodes.Success)
        {
            repo.Dispose();
            throw new System.InvalidOperationException(
                $"add-exclude for setup failed: exit={acode} stderr={ase}");
        }
        return (repo, port);
    }

    [Fact]
    public void RemoveSystemKind_WithoutOverride_ExitsWith21()
    {
        var (repo, port) = BuildAndInit_WithSystemExclusions();
        using (repo)
        {
            var (code, _, se) = Run(
                new[] { "--remove-exclude", "archive_2024", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.RemoveExcludeSystemKindRefused, code);
            Assert.Contains("archive_2024", se);
            Assert.Contains("kind='pattern'", se);
            Assert.Contains("--allow-system-exclusions", se);

            // Workspace state unchanged: archive_2024 still excluded.
            var (xcode, xso, _) = Run(
                new[] { "--Exclusions", "--bridge-port", port.ToString() }, repo.Root);
            Assert.Equal(ExitCodes.Success, xcode);
            Assert.Contains("archive_2024", xso);
        }
    }

    [Fact]
    public void RemoveSystemKind_WithoutOverride_JsonEnvelopeNamesOffenders()
    {
        var (repo, port) = BuildAndInit_WithSystemExclusions();
        using (repo)
        {
            var (code, so, _) = Run(
                new[] { "--remove-exclude", "archive_2024", "--json", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.RemoveExcludeSystemKindRefused, code);
            using var doc = JsonDocument.Parse(so);
            Assert.Equal("error", doc.RootElement.GetProperty("result").GetString());
            Assert.Equal(21, doc.RootElement.GetProperty("code").GetInt32());
            var offenders = doc.RootElement.GetProperty("offenders");
            Assert.Equal(1, offenders.GetArrayLength());
            Assert.Equal("archive_2024", offenders[0].GetProperty("path").GetString());
            Assert.Equal("pattern", offenders[0].GetProperty("kind").GetString());
        }
    }

    [Fact]
    public void RemoveSystemKind_WithOverride_Succeeds()
    {
        var (repo, port) = BuildAndInit_WithSystemExclusions();
        using (repo)
        {
            var (code, so, _) = Run(
                new[] { "--remove-exclude", "archive_2024",
                        "--allow-system-exclusions",
                        "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("archive_2024 (kind: pattern)", so);
            Assert.Contains("inserted:               1 dart_files row(s)", so);

            // archive_2024 is gone from the exclusion list.
            var (xcode, xso, _) = Run(
                new[] { "--Exclusions", "--bridge-port", port.ToString() }, repo.Root);
            Assert.Equal(ExitCodes.Success, xcode);
            Assert.DoesNotContain("\narchive_2024\n", "\n" + xso);
        }
    }

    [Fact]
    public void MixedBatch_NoOverride_AllOrNothingRejection()
    {
        var (repo, port) = BuildAndInit_WithSystemExclusions();
        using (repo)
        {
            // Mix: one system-kind, one manual. Without override, BOTH must
            // be left in the exclusion list (all-or-nothing).
            var (code, _, _) = Run(
                new[] { "--remove-exclude", "archive_2024",
                        "--remove-exclude", "extra",
                        "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.RemoveExcludeSystemKindRefused, code);

            // Both still excluded.
            var (xcode, xso, _) = Run(
                new[] { "--Exclusions", "--bridge-port", port.ToString() }, repo.Root);
            Assert.Equal(ExitCodes.Success, xcode);
            Assert.Contains("archive_2024", xso);
            Assert.Contains("extra", xso);
        }
    }
}
