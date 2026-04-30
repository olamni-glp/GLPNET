using D2Net.Scaffold;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

public class DartSrcRenameTests
{
    [Fact]
    public void SimpleDartFile_IsRenamedWithSrcSuffix()
    {
        using var fx = new FixtureBuilder();
        var src = "class Runner {}\n";
        fx.AddDartFile("lib/runner.dart", src);

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        Assert.Equal(0, outcome.ExitCode);
        Assert.True(File.Exists(fx.TargetPath("lib/runner.dart.src")));
        Assert.False(File.Exists(fx.TargetPath("lib/runner.dart")),
            "Original .dart filename must NOT exist in the target (it has been renamed to .dart.src).");
        Assert.Equal(File.ReadAllText(fx.SourcePath("lib/runner.dart")),
                     File.ReadAllText(fx.TargetPath("lib/runner.dart.src")));
    }

    [Fact]
    public void MultiDotDartFile_OnlyTrailingDotDartIsExtended()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/foo.bar.dart", "class FooBar {}\n");

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        Assert.Equal(0, outcome.ExitCode);
        Assert.True(File.Exists(fx.TargetPath("lib/foo.bar.dart.src")));
        // Companion files use the basename without the trailing .dart, so basename = "foo.bar"
        Assert.True(File.Exists(fx.TargetPath("lib/foo.bar.cs")));
        Assert.True(File.Exists(fx.TargetPath("lib/foo.bar.ana")));
    }

    [Fact]
    public void DartSrc_IsByteIdenticalToSource_IncludingBinaryBytes()
    {
        using var fx = new FixtureBuilder();
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0xFE, 0xFF, 0x0A, 0x0D };
        fx.AddBinaryFile("lib/weird.dart", bytes);

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        Assert.Equal(0, outcome.ExitCode);
        var copied = File.ReadAllBytes(fx.TargetPath("lib/weird.dart.src"));
        Assert.Equal(bytes, copied);
    }
}
