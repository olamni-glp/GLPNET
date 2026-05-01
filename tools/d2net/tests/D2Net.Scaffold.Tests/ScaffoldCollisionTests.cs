using System.IO;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

/// <summary>
/// T027: pre-walk collision detection (FR-013).
/// </summary>
public class ScaffoldCollisionTests
{
    [Fact]
    public void DartAndPreExistingNonEmptyWorkdir_InSource_RejectsWithExit25()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("extra/glp_repl.dart", "// repl")
            // Pre-create a __glp_repl/ directory in the source with a file in it.
            .AddSourceFile("extra/__glp_repl/leftover.txt", "old content");

        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        var (code, _, se) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.ScaffoldWorkdirCollision, code);
        Assert.Contains("__<basename> collision", se);
        Assert.Contains("__glp_repl", se);

        // Target untouched.
        var targetRoot = Path.Combine(repo.Root, "glp_runtime_net");
        Assert.False(Directory.Exists(targetRoot));
    }

    [Fact]
    public void DartFileNextToWorkdirNamedFile_InSource_RejectsWithExit25()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("extra/glp_repl.dart", "// repl")
            // Real file at the colliding path.
            .AddSourceFile("extra/__glp_repl", "this is a file, not a directory");

        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        var (code, _, se) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.ScaffoldWorkdirCollision, code);
        Assert.Contains("__glp_repl", se);
    }

    [Fact]
    public void DartAndEmptyWorkdir_InSource_NotACollision()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("extra/glp_repl.dart", "// repl")
            // Empty pre-existing __glp_repl is benign — scaffold just owns it.
            .AddDirectory("extra/__glp_repl");

        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        var (code, _, _) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.Success, code);
    }
}
