using System.IO;

namespace D2Net.Scaffold.Tests;

/// <summary>
/// T013: ArgParser tests for d2net-scaffold's CLI surface
/// (per contracts/scaffold-cli-contract.md and FR-017 / FR-012a).
/// These tests do NOT spawn the bridge; they exercise Program.Run only as far
/// as the parser and exit-code mapping, with stdin closed.
/// </summary>
public class ScaffoldArgParserTests
{
    private static (int Code, string Stdout, string Stderr) RunArgs(string[] args, string cwd = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        // Use a non-existent CWD so we never reach the bridge — preflight will short-circuit.
        var workingDir = string.IsNullOrEmpty(cwd)
            ? Path.Combine(Path.GetTempPath(), "d2net-scaffold-noworkspace-" + System.Guid.NewGuid().ToString("N"))
            : cwd;
        Directory.CreateDirectory(workingDir);
        try
        {
            var code = Program.Run(args, new StringReader(""), so, se, workingDir);
            return (code, so.ToString(), se.ToString());
        }
        finally
        {
            try { Directory.Delete(workingDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Help_ExitsZero_AndPrintsUsage()
    {
        var (code, so, _) = RunArgs(new[] { "--help" });
        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("d2net-scaffold", so);
        Assert.Contains("--FORCE --DELETE-TARGET", so);
    }

    [Fact]
    public void Version_ExitsZero_AndPrintsVersion()
    {
        var (code, so, _) = RunArgs(new[] { "--version" });
        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("d2net-scaffold", so);
        Assert.Matches(@"\d+\.\d+", so);
    }

    [Fact]
    public void EmptyArgs_DoNotMapToArgumentError()
    {
        // Empty args are valid; preflight will fail with workspace-missing (22), not 1.
        var (code, _, _) = RunArgs(System.Array.Empty<string>());
        Assert.Equal(ExitCodes.ScaffoldWorkspaceMissing, code);
    }

    [Fact]
    public void JsonAlone_DoesNotMapToArgumentError()
    {
        var (code, _, _) = RunArgs(new[] { "--json" });
        // Same: parses fine; preflight fails with workspace-missing (22).
        Assert.Equal(ExitCodes.ScaffoldWorkspaceMissing, code);
    }

    [Fact]
    public void ForceWithoutDeleteTarget_IsArgumentError()
    {
        var (code, _, se) = RunArgs(new[] { "--FORCE" });
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("must be supplied together", se);
    }

    [Fact]
    public void DeleteTargetWithoutForce_IsArgumentError()
    {
        var (code, _, se) = RunArgs(new[] { "--DELETE-TARGET" });
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("must be supplied together", se);
    }

    [Fact]
    public void ForceAndDeleteTargetTogether_DoesNotMapToArgumentError()
    {
        var (code, _, _) = RunArgs(new[] { "--FORCE", "--DELETE-TARGET" });
        Assert.Equal(ExitCodes.ScaffoldWorkspaceMissing, code);
    }

    [Fact]
    public void PositionalArg_IsArgumentError()
    {
        var (code, _, se) = RunArgs(new[] { "some-source", "some-target" });
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("positional arguments are not accepted", se);
    }

    [Fact]
    public void UnknownFlag_IsArgumentError()
    {
        var (code, _, se) = RunArgs(new[] { "--refresh" });
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("unknown flag", se);
    }

    [Fact]
    public void HelpWithJson_IsArgumentError()
    {
        var (code, _, se) = RunArgs(new[] { "--help", "--json" });
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("mutually exclusive", se);
    }

    [Fact]
    public void Help_BlockMentionsDestructiveFlagPair()
    {
        var (_, so, _) = RunArgs(new[] { "--help" });
        Assert.Contains("--FORCE --DELETE-TARGET", so);
        Assert.Contains("interactive confirmation", so);
        Assert.Contains("idempotent", so.ToLowerInvariant());
    }

    [Fact]
    public void Help_BlockMentionsAtomicityAndStaging()
    {
        var (_, so, _) = RunArgs(new[] { "--help" });
        Assert.Contains(".d2net-tmp", so);
        Assert.Contains("commits", so);
    }
}
