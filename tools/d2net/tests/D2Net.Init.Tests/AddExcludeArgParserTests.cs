using System.IO;
using D2Net.Init;

namespace D2Net.Init.Tests;

public class AddExcludeArgParserTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    [Fact]
    public void AddExcludeWithoutValueIsArgumentError()
    {
        var (code, _, se) = Run(new[] { "--add-exclude" }, Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("--add-exclude requires a value", se);
    }

    [Fact]
    public void AddExcludeRejectedWithInitFlags()
    {
        var (code, _, se) = Run(
            new[] { "--add-exclude", "foo", "--source", "glp_runtime" },
            Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("init flags", se);
    }

    [Fact]
    public void AddExcludeRejectedWithListFlag()
    {
        var (code, _, se) = Run(
            new[] { "--add-exclude", "foo", "--list" },
            Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("--list", se);
    }

    [Fact]
    public void AddExcludeRejectedWithExclusionsFlag()
    {
        var (code, _, se) = Run(
            new[] { "--add-exclude", "foo", "--Exclusions" },
            Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
    }

    [Fact]
    public void AddExcludeRejectedWithNonInteractive()
    {
        var (code, _, se) = Run(
            new[] { "--add-exclude", "foo", "--non-interactive" },
            Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("--non-interactive", se);
    }

    [Fact]
    public void AddExcludeWithoutWorkspaceFailsWithCode6()
    {
        // No .D2NET/ at the temp directory; no need to spawn the bridge.
        var temp = Path.Combine(Path.GetTempPath(), "d2net-addexclude-noworkspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var (code, _, se) = Run(new[] { "--add-exclude", "foo" }, temp);
            Assert.Equal(ExitCodes.WorkspaceMissingForInspection, code);
            Assert.Contains("no D2NET workspace", se);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }

    [Fact]
    public void AddExcludeWorkspaceMissingJsonEmitsErrorEnvelope()
    {
        var temp = Path.Combine(Path.GetTempPath(), "d2net-addexclude-noworkspace-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var (code, so, _) = Run(new[] { "--add-exclude", "foo", "--json" }, temp);
            Assert.Equal(ExitCodes.WorkspaceMissingForInspection, code);
            Assert.Contains("\"result\":\"error\"", so);
            Assert.Contains("\"code\":6", so);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }
}
