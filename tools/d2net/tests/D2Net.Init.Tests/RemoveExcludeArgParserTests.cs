using System.IO;
using D2Net.Init;

namespace D2Net.Init.Tests;

public class RemoveExcludeArgParserTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    [Fact]
    public void RemoveExcludeWithoutValueIsArgumentError()
    {
        var (code, _, se) = Run(new[] { "--remove-exclude" }, Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("--remove-exclude requires a value", se);
    }

    [Fact]
    public void RemoveExcludeWithAddExcludeIsArgumentError()
    {
        var (code, _, se) = Run(
            new[] { "--remove-exclude", "foo", "--add-exclude", "bar" },
            Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("--add-exclude and --remove-exclude cannot be combined", se);
    }

    [Fact]
    public void RemoveExcludeWithListIsArgumentError()
    {
        var (code, _, se) = Run(
            new[] { "--remove-exclude", "foo", "--list" },
            Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
    }

    [Fact]
    public void RemoveExcludeWithInitFlagIsArgumentError()
    {
        var (code, _, se) = Run(
            new[] { "--remove-exclude", "foo", "--source", "x" },
            Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("init flags", se);
    }

    [Fact]
    public void AllowSystemExclusionsWithoutRemoveExcludeIsArgumentError()
    {
        var (code, _, se) = Run(
            new[] { "--allow-system-exclusions" },
            Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("--allow-system-exclusions is only valid with --remove-exclude", se);
    }

    [Fact]
    public void RemoveExcludeWithoutWorkspaceFailsWithCode6()
    {
        var temp = Path.Combine(Path.GetTempPath(), "d2net-rm-noworkspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var (code, _, se) = Run(new[] { "--remove-exclude", "foo" }, temp);
            Assert.Equal(ExitCodes.WorkspaceMissingForInspection, code);
            Assert.Contains("no D2NET workspace", se);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RemoveExcludeNoWorkspaceJsonEmitsErrorEnvelope()
    {
        var temp = Path.Combine(Path.GetTempPath(), "d2net-rm-noworkspace-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var (code, so, _) = Run(new[] { "--remove-exclude", "foo", "--json" }, temp);
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
