using D2Net.Scaffold;
using D2Net.Scaffold.Models;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

public class CompanionStubTests
{
    [Fact]
    public void EachDartFile_ProducesNineCompanionFiles_WithCorrectExtensions()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/runner.dart", "class Runner {}");

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        Assert.Equal(0, outcome.ExitCode);
        foreach (var ext in CompanionExtensions.All)
        {
            var path = fx.TargetPath($"lib/runner.{ext}");
            Assert.True(File.Exists(path), $"Companion file lib/runner.{ext} must exist.");
        }
        Assert.Equal(9, CompanionExtensions.All.Count);
        Assert.Equal(9, outcome.Summary!.CompanionStubsWritten);
    }

    [Fact]
    public void EveryCompanion_ContainsTodoComment_ReferencingBaseNameAndExtension()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/runner.dart", "class Runner {}");

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));
        Assert.Equal(0, outcome.ExitCode);

        foreach (var ext in CompanionExtensions.All)
        {
            var path = fx.TargetPath($"lib/runner.{ext}");
            var content = File.ReadAllText(path).TrimEnd();
            Assert.StartsWith("// TODO: d2net", content);
            Assert.Contains("runner.dart.src", content);
            Assert.Contains($"artifact: {ext}", content);
        }
    }

    [Fact]
    public void CompanionFiles_AreNonEmpty()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/runner.dart", "class Runner {}");

        ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        foreach (var ext in CompanionExtensions.All)
        {
            var path = fx.TargetPath($"lib/runner.{ext}");
            Assert.True(new FileInfo(path).Length > 0,
                $"Companion {ext} must be non-empty (carries the TODO line).");
        }
    }
}
