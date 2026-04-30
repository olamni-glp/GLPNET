using System.IO;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

public class WorkspaceMissingForInspectionTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    [Fact]
    public void ListWithoutWorkspaceExitsSix()
    {
        using var repo = new TempRepoBuilder();
        var (code, _, se) = Run(new[] { "--list" }, repo.Root);
        Assert.Equal(ExitCodes.WorkspaceMissingForInspection, code);
        Assert.Contains("no D2NET workspace found", se);
    }

    [Fact]
    public void ExclusionsWithoutWorkspaceExitsSix()
    {
        using var repo = new TempRepoBuilder();
        var (code, _, se) = Run(new[] { "--Exclusions" }, repo.Root);
        Assert.Equal(ExitCodes.WorkspaceMissingForInspection, code);
        Assert.Contains("no D2NET workspace found", se);
    }

    [Fact]
    public void CurrentPhaseWithoutWorkspaceExitsSix()
    {
        using var repo = new TempRepoBuilder();
        var (code, _, se) = Run(new[] { "--current-phase" }, repo.Root);
        Assert.Equal(ExitCodes.WorkspaceMissingForInspection, code);
        Assert.Contains("no D2NET workspace found", se);
    }
}
