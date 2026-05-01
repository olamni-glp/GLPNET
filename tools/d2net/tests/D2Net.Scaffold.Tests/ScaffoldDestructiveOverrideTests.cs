using System.IO;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

/// <summary>
/// T026: --FORCE --DELETE-TARGET interactive flow.
/// </summary>
public class ScaffoldDestructiveOverrideTests
{
    [Fact]
    public void TargetExistsNotManaged_NoOverride_ExitsWith24()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart", "// runner");
        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        // Hand-create a target dir with non-scaffold content.
        var targetRoot = Path.Combine(repo.Root, "glp_runtime_net");
        Directory.CreateDirectory(targetRoot);
        File.WriteAllText(Path.Combine(targetRoot, "manual.txt"), "user content");

        var (code, _, se) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.ScaffoldTargetNotEmptyAndNotManaged, code);
        Assert.Contains("not produced by a prior scaffold run", se);
        // Target untouched.
        Assert.True(File.Exists(Path.Combine(targetRoot, "manual.txt")));
    }

    [Fact]
    public void TargetExistsNotManaged_WithOverride_ConfirmYes_ProceedsAndRebuilds()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart", "// runner");
        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        var targetRoot = Path.Combine(repo.Root, "glp_runtime_net");
        Directory.CreateDirectory(targetRoot);
        File.WriteAllText(Path.Combine(targetRoot, "manual.txt"), "user content");

        var (code, so, se) = InitHelper.Scaffold(repo.Root, port, forceDelete: true, stdin: "yes\n");
        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains(targetRoot, se);
        Assert.Contains("Proceed? (yes/no)", se);
        Assert.Contains("destructive override", so);

        Assert.True(Directory.Exists(targetRoot));
        Assert.False(File.Exists(Path.Combine(targetRoot, "manual.txt"))); // wiped
        Assert.True(File.Exists(Path.Combine(targetRoot, "lib", "runner.dart"))); // re-laid down
        Assert.True(Directory.Exists(Path.Combine(targetRoot, "lib", "__runner")));
    }

    [Fact]
    public void TargetExistsNotManaged_WithOverride_ConfirmNo_ExitsWith29()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart", "// runner");
        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        var targetRoot = Path.Combine(repo.Root, "glp_runtime_net");
        Directory.CreateDirectory(targetRoot);
        var manualPath = Path.Combine(targetRoot, "manual.txt");
        var manualContent = "user content";
        File.WriteAllText(manualPath, manualContent);

        var (code, _, _) = InitHelper.Scaffold(repo.Root, port, forceDelete: true, stdin: "no\n");
        Assert.Equal(ExitCodes.ScaffoldOperatorCancelledTargetDeletion, code);
        // Target byte-identical.
        Assert.True(File.Exists(manualPath));
        Assert.Equal(manualContent, File.ReadAllText(manualPath));
    }

    [Fact]
    public void TargetDoesNotExist_WithOverride_NoPrompt_ProceedsNormally()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart", "// runner");
        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        var targetRoot = Path.Combine(repo.Root, "glp_runtime_net");
        Assert.False(Directory.Exists(targetRoot));

        var (code, _, se) = InitHelper.Scaffold(repo.Root, port, forceDelete: true);
        Assert.Equal(ExitCodes.Success, code);
        // No prompt fired.
        Assert.DoesNotContain("Proceed? (yes/no)", se);
        Assert.True(Directory.Exists(targetRoot));
    }
}
