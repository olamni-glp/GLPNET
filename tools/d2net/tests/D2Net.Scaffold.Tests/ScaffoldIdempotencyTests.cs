using System.IO;
using System.Linq;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

/// <summary>
/// T022: idempotent re-run.
/// </summary>
public class ScaffoldIdempotencyTests
{
    [Fact]
    public void Scaffold_RunTwice_NoOpSecondRun()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart", "// runner")
            .AddDartFile("lib/heap.dart", "// heap")
            .AddSourceFile("lib/pubspec.yaml", "name: test\n");

        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        var (c1, so1, _) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.Success, c1);

        // Snapshot pre-second-run state of target tree files.
        var targetRoot = Path.Combine(repo.Root, "glp_runtime_net");
        var beforeFiles = Directory.EnumerateFiles(targetRoot, "*", SearchOption.AllDirectories)
            .ToDictionary(f => f, f => File.ReadAllBytes(f));

        var (c2, so2, _) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.Success, c2);
        Assert.Contains("added paths   : 0", so2);
        Assert.Contains("removed paths : 0", so2);

        // Files byte-identical post-second-run.
        var afterFiles = Directory.EnumerateFiles(targetRoot, "*", SearchOption.AllDirectories)
            .ToDictionary(f => f, f => File.ReadAllBytes(f));
        Assert.Equal(beforeFiles.Keys.OrderBy(k => k), afterFiles.Keys.OrderBy(k => k));
        foreach (var (path, content) in beforeFiles)
            Assert.Equal(content, afterFiles[path]);
    }
}
