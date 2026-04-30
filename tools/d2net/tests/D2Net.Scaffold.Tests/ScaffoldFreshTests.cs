using System.Text.Json;
using D2Net.Scaffold;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

public class ScaffoldFreshTests
{
    [Fact]
    public void EndToEnd_FreshScaffold_HonoursEverySpecInvariant()
    {
        using var fx = new FixtureBuilder();
        // Mixed Dart and non-Dart at multiple depths, with a pruned build/ dir.
        fx.AddDartFile("lib/runner.dart", "class Runner {}\n");
        fx.AddDartFile("lib/util/helper.dart", "class Helper {}\n");
        fx.AddDartFile("lib/util/extra.dart", "class Extra {}\n");
        fx.AddFile("pubspec.yaml", "name: glp_runtime\n");
        fx.AddFile("README.md", "# glp_runtime\n");
        fx.AddFile("lib/data.json", "{\"key\":\"value\"}\n");
        fx.AddBinaryFile("assets/logo.png", new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        // Pruned content (must not survive)
        fx.AddDartFile("build/cache.dart", "skip");
        fx.AddFile("build/manifest.json", "skip");

        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));
        Assert.Equal(0, outcome.ExitCode);

        // SC-003: 3 dart files outside pruned dirs
        Assert.Equal(3, outcome.Summary!.DartSrcFilesWritten);
        Assert.True(File.Exists(fx.TargetPath("lib/runner.dart.src")));
        Assert.True(File.Exists(fx.TargetPath("lib/util/helper.dart.src")));
        Assert.True(File.Exists(fx.TargetPath("lib/util/extra.dart.src")));
        Assert.False(File.Exists(fx.TargetPath("build/cache.dart.src")));

        // SC-002: non-Dart files copied byte-for-byte
        Assert.Equal(File.ReadAllText(fx.SourcePath("pubspec.yaml")),
                     File.ReadAllText(fx.TargetPath("pubspec.yaml")));
        Assert.Equal(File.ReadAllBytes(fx.SourcePath("assets/logo.png")),
                     File.ReadAllBytes(fx.TargetPath("assets/logo.png")));

        // SC-004: 9 companions per dart file
        Assert.Equal(3 * 9, outcome.Summary.CompanionStubsWritten);

        // SC-006: tracker file exists, parses, has 3 records
        var trackerPath = fx.TargetPath("d2net-tracker.json");
        Assert.True(File.Exists(trackerPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(trackerPath));
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(3, doc.RootElement.GetArrayLength());

        // SC-001: no pruned directory survives
        Assert.False(Directory.Exists(fx.TargetPath("build")));
    }
}
