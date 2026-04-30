using D2Net.Scaffold;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

public class ExitCodeTests
{
    [Fact]
    public void SourceMissing_ReturnsExitCode2()
    {
        using var fx = new FixtureBuilder();
        var bogusSource = Path.Combine(fx.Root, "does-not-exist");
        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(bogusSource, fx.TargetRoot, false));
        Assert.Equal(2, outcome.ExitCode);
    }

    [Fact]
    public void TargetExists_ReturnsExitCode3_AndDoesNotTouchExisting()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/a.dart", "// a");
        Directory.CreateDirectory(fx.TargetRoot);
        File.WriteAllText(Path.Combine(fx.TargetRoot, "marker.txt"), "PRE-EXISTING");

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        Assert.Equal(3, outcome.ExitCode);
        Assert.Equal("PRE-EXISTING", File.ReadAllText(Path.Combine(fx.TargetRoot, "marker.txt")));
    }

    [Fact]
    public void TargetIsNestedInsideSource_ReturnsExitCode4()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/a.dart", "// a");
        var nestedTarget = Path.Combine(fx.SourceRoot, "nested-target");
        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, nestedTarget, false));
        Assert.Equal(4, outcome.ExitCode);
    }

    [Fact]
    public void TargetEqualsSource_ReturnsExitCode4()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/a.dart", "// a");
        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.SourceRoot, false));
        Assert.Equal(4, outcome.ExitCode);
    }

    [Fact]
    public void Refresh_WithMissingTarget_ReturnsExitCode6()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/a.dart", "// a");
        var outcome = RefreshRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, true));
        Assert.Equal(6, outcome.ExitCode);
    }
}
