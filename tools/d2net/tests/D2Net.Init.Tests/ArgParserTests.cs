using System.IO;
using System.Reflection;
using D2Net.Init;

namespace D2Net.Init.Tests;

/// <summary>
/// Exercises the public Program.Run entry point with synthetic args. We use
/// reflection-via-InternalsVisibleTo would be cleaner, but Run() returns
/// observable exit codes via stdout/stderr capture so we just call it.
/// </summary>
public class ArgParserTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    [Fact]
    public void HelpExitsZero()
    {
        var (code, so, _) = Run(new[] { "--help" }, Path.GetTempPath());
        Assert.Equal(0, code);
        Assert.Contains("Usage:", so);
    }

    [Fact]
    public void VersionExitsZero()
    {
        var (code, so, _) = Run(new[] { "--version" }, Path.GetTempPath());
        Assert.Equal(0, code);
        Assert.Contains("d2net-init", so);
    }

    [Fact]
    public void UnknownFlagIsArgumentError()
    {
        var (code, _, se) = Run(new[] { "--bogus" }, Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("unknown argument", se);
    }

    [Fact]
    public void ForceWithoutDeleteIsArgumentError()
    {
        var (code, _, se) = Run(new[] { "--FORCE" }, Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("must be supplied together", se);
    }

    [Fact]
    public void DeleteWithoutForceIsArgumentError()
    {
        var (code, _, se) = Run(new[] { "--DELETE-EXISTING" }, Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("must be supplied together", se);
    }

    [Fact]
    public void TwoInspectionFlagsIsArgumentError()
    {
        var (code, _, se) = Run(new[] { "--list", "--Exclusions" }, Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("mutually exclusive", se);
    }

    [Fact]
    public void InspectionWithInitFlagsIsArgumentError()
    {
        var (code, _, se) = Run(new[] { "--list", "--source", "x" }, Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("inspection options cannot be combined", se);
    }

    [Fact]
    public void BadBridgePortIsArgumentError()
    {
        var (code, _, se) = Run(new[] { "--bridge-port", "70000" }, Path.GetTempPath());
        Assert.Equal(ExitCodes.ArgumentError, code);
        Assert.Contains("--bridge-port must be an integer", se);
    }

    [Fact]
    public void WrongCwdReturnsExit2()
    {
        // A fresh empty temp dir has no .git/, no .D2NET/, and no source dir.
        var tmp = Path.Combine(Path.GetTempPath(), "d2net-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            var (code, _, se) = Run(
                new[] { "--source", "ghost", "--target-extension", "_net", "--target", "ghost_net",
                        "--accept-suggested-exclusions", "--non-interactive" },
                tmp);
            Assert.Equal(ExitCodes.WrongCwd, code);
            Assert.Contains("does not look like a D2NET repository root", se);
        }
        finally { Directory.Delete(tmp, recursive: true); }
    }
}
