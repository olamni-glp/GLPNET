using D2Net.Scaffold;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

public class PrunedDirectoriesTests
{
    [Theory]
    [InlineData(".dart_tool")]
    [InlineData("build")]
    [InlineData(".git")]
    [InlineData(".idea")]
    [InlineData(".vscode")]
    public void PrunedDirectory_AtTopLevel_IsNotMirrored(string prunedName)
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/runner.dart", "class Runner {}");
        fx.AddFile($"{prunedName}/should_not_appear.txt", "skip me");
        fx.AddDartFile($"{prunedName}/dart_in_pruned.dart", "skip me too");

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        Assert.Equal(0, outcome.ExitCode);
        Assert.False(Directory.Exists(fx.TargetPath(prunedName)),
            $"Pruned directory '{prunedName}' must not exist in the target.");
        Assert.False(File.Exists(fx.TargetPath($"{prunedName}/should_not_appear.txt")));
        Assert.False(File.Exists(fx.TargetPath($"{prunedName}/dart_in_pruned.dart.src")));
        // Sanity: the non-pruned dart file IS mirrored.
        Assert.True(File.Exists(fx.TargetPath("lib/runner.dart.src")));
    }

    [Fact]
    public void PrunedDirectory_NestedDeep_IsAlsoSkipped()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/runner.dart", "class Runner {}");
        fx.AddDartFile("lib/sub/.dart_tool/cache.dart", "skip me");
        fx.AddFile("lib/sub/.dart_tool/manifest.json", "skip me");

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        Assert.Equal(0, outcome.ExitCode);
        Assert.False(Directory.Exists(fx.TargetPath("lib/sub/.dart_tool")));
        Assert.True(File.Exists(fx.TargetPath("lib/runner.dart.src")));
    }

    [Fact]
    public void TrackerArrayLength_EqualsDartFileCountOutsidePrunedDirectories()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/a.dart", "// a");
        fx.AddDartFile("lib/b.dart", "// b");
        fx.AddDartFile(".dart_tool/excluded.dart", "// excluded");
        fx.AddDartFile("build/also_excluded.dart", "// also excluded");

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(2, outcome.Summary!.TrackerRecordsWritten);
        Assert.Equal(2, outcome.Summary.DartSrcFilesWritten);
    }
}
