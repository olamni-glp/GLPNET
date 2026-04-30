using D2Net.Scaffold;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

public class PreflightCollisionTests
{
    [Fact]
    public void CollisionWithExistingDotCs_AbortsWithExitCode5_AndWritesNothing()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/runner.dart", "class Runner {}");
        fx.AddFile("lib/runner.cs", "// pre-existing C# file in source — would collide with generated stub");

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        Assert.Equal(5, outcome.ExitCode);
        Assert.Single(outcome.Collisions);
        var c = outcome.Collisions[0];
        Assert.Equal("lib/runner.dart", c.DartFileRelPath);
        Assert.Equal("cs", c.CollidingExtension);
        Assert.Equal("lib/runner.cs", c.ExistingFileRelPath);

        Assert.False(Directory.Exists(fx.TargetRoot),
            "Target tree must be untouched on pre-flight collision.");
    }

    [Fact]
    public void MultipleCollisions_AllReported_BeforeAbort()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/runner.dart", "class Runner {}");
        fx.AddFile("lib/runner.cs", "// collision 1");
        fx.AddFile("lib/runner.dep", "// collision 2");
        fx.AddFile("lib/runner.tst", "// collision 3");

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        Assert.Equal(5, outcome.ExitCode);
        Assert.Equal(3, outcome.Collisions.Count);
        var exts = outcome.Collisions.Select(c => c.CollidingExtension).ToHashSet();
        Assert.Contains("cs", exts);
        Assert.Contains("dep", exts);
        Assert.Contains("tst", exts);
    }
}
