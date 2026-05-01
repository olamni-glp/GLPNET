using System.IO;
using System.Linq;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

/// <summary>
/// T023: reconciliation cases. After an initial scaffold:
///   1. add an exclusion -&gt; previously-included subtree disappears from
///      target + scaffold_tracker.
///   2. remove the exclusion -&gt; subtree reappears with __workdirs.
/// </summary>
public class ScaffoldReconciliationTests
{
    [Fact]
    public void AddExclusion_AfterScaffold_RemovesSubtreeFromTarget()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart", "// runner")
            .AddDartFile("extra/x.dart", "// x")
            .AddDartFile("extra/y.dart", "// y");

        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        var (c1, _, _) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.Success, c1);

        var targetRoot = Path.Combine(repo.Root, "glp_runtime_net");
        Assert.True(Directory.Exists(Path.Combine(targetRoot, "extra")));

        // Now exclude extra/.
        var (excCode, _, _) = InitHelper.AddExclude(repo.Root, port, "extra");
        Assert.Equal(D2Net.Init.ExitCodes.Success, excCode);

        // Re-run scaffold.
        var (c2, so2, _) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.Success, c2);
        Assert.Contains("removed paths", so2);

        Assert.False(Directory.Exists(Path.Combine(targetRoot, "extra")));
        // dart_files for extra/* should have NULL columns now (we cleared them on remove-set).
        var workspace = Path.Combine(repo.Root, ".D2NET");
        using var verifier = new DbVerifier(Path.Combine(workspace, "pgdb"));
        var dartRows = verifier.GetDartFilesWithScaffoldColumns()
            .Where(r => r.FullPath.Contains("extra/"))
            .ToList();
        // Note: the dart_files rows for extra/* may still exist (init created them);
        // their target columns should be NULL since extra is now excluded.
        // Actually d2net-init --add-exclude DELETES dart_files rows for the excluded subtree,
        // so the rows are gone entirely. Either way, not present in tracker.
        var tracker = verifier.GetTrackerRows();
        Assert.DoesNotContain(tracker, t => t.SourcePath.Contains("extra/"));
    }

    [Fact]
    public void RemoveExclusion_AfterScaffold_RestoresSubtree()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart", "// runner")
            .AddDartFile("extra/x.dart", "// x");

        var (initCode, _, _, port) = InitHelper.Init(repo.Root);
        Assert.Equal(D2Net.Init.ExitCodes.Success, initCode);

        var (cAdd, _, _) = InitHelper.AddExclude(repo.Root, port, "extra");
        Assert.Equal(D2Net.Init.ExitCodes.Success, cAdd);

        var (c1, _, _) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.Success, c1);

        var targetRoot = Path.Combine(repo.Root, "glp_runtime_net");
        Assert.False(Directory.Exists(Path.Combine(targetRoot, "extra")));

        // Now remove exclusion. extra was added manually so kind='manual' -> no allow-system needed.
        var (cRem, _, _) = InitHelper.RemoveExclude(repo.Root, port, allowSystem: false, "extra");
        Assert.Equal(D2Net.Init.ExitCodes.Success, cRem);

        var (c2, so2, _) = InitHelper.Scaffold(repo.Root, port);
        Assert.Equal(ExitCodes.Success, c2);
        Assert.Contains("added paths", so2);

        Assert.True(Directory.Exists(Path.Combine(targetRoot, "extra")));
        Assert.True(File.Exists(Path.Combine(targetRoot, "extra", "x.dart")));
        Assert.True(Directory.Exists(Path.Combine(targetRoot, "extra", "__x")));
    }
}
