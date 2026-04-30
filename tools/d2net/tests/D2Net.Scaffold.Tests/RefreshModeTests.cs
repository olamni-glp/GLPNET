using D2Net.Scaffold;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

public class RefreshModeTests
{
    [Fact]
    public void Refresh_PreservesEditedCompanionFiles_ByteForByte()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/runner.dart", "class Runner {}\n");

        // Fresh scaffold first
        var first = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));
        Assert.Equal(0, first.ExitCode);

        // User edits the .cs companion to contain real C# code
        var csPath = fx.TargetPath("lib/runner.cs");
        var realCode = "namespace Glp;\npublic class Runner { /* ported */ }\n";
        File.WriteAllText(csPath, realCode);

        // User also edits the .dart source
        File.WriteAllText(fx.SourcePath("lib/runner.dart"), "class Runner { /* changed */ }\n");

        // Re-run with --refresh
        var refresh = RefreshRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, true));
        Assert.Equal(0, refresh.ExitCode);

        // .cs companion preserved byte-identical
        Assert.Equal(realCode, File.ReadAllText(csPath));

        // .dart.src refreshed to current source
        Assert.Equal("class Runner { /* changed */ }\n",
                     File.ReadAllText(fx.TargetPath("lib/runner.dart.src")));
    }

    [Fact]
    public void Refresh_LeavesTrackerByteIdentical()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/a.dart", "// a");
        ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        var trackerPath = fx.TargetPath("d2net-tracker.json");
        var beforeBytes = File.ReadAllBytes(trackerPath);

        // Edit source so refresh has real work to do
        File.WriteAllText(fx.SourcePath("lib/a.dart"), "// a edited");

        var refresh = RefreshRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, true));
        Assert.Equal(0, refresh.ExitCode);

        var afterBytes = File.ReadAllBytes(trackerPath);
        Assert.Equal(beforeBytes, afterBytes);
        Assert.Equal(0, refresh.Summary!.TrackerRecordsWritten);
    }

    [Fact]
    public void Refresh_NewlyAddedDartFile_GetsCompanionStubs_ButNoTrackerEntry()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/a.dart", "// a");
        ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        // Capture pre-refresh tracker
        var trackerPath = fx.TargetPath("d2net-tracker.json");
        var beforeBytes = File.ReadAllBytes(trackerPath);

        // Add a new Dart file in the source after the fresh run
        File.WriteAllText(fx.SourcePath("lib/new_file.dart"), "// new\n");

        var refresh = RefreshRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, true));
        Assert.Equal(0, refresh.ExitCode);

        // New companion stubs created
        Assert.True(File.Exists(fx.TargetPath("lib/new_file.cs")));
        Assert.True(File.Exists(fx.TargetPath("lib/new_file.dart.src")));

        // Reported as newly-discovered
        Assert.Contains("lib/new_file.dart", refresh.Summary!.NewlyDiscoveredDartFiles);

        // Tracker still byte-identical (no entry added)
        Assert.Equal(beforeBytes, File.ReadAllBytes(trackerPath));
    }

    [Fact]
    public void Refresh_NoSourceChanges_LeavesEverythingByteIdentical()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/a.dart", "// a\n");
        fx.AddFile("README.md", "# r\n");
        ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        var aSrcBefore = File.ReadAllBytes(fx.TargetPath("lib/a.dart.src"));
        var readmeBefore = File.ReadAllBytes(fx.TargetPath("README.md"));
        var csBefore = File.ReadAllBytes(fx.TargetPath("lib/a.cs"));
        var trackerBefore = File.ReadAllBytes(fx.TargetPath("d2net-tracker.json"));

        var refresh = RefreshRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, true));
        Assert.Equal(0, refresh.ExitCode);

        Assert.Equal(aSrcBefore, File.ReadAllBytes(fx.TargetPath("lib/a.dart.src")));
        Assert.Equal(readmeBefore, File.ReadAllBytes(fx.TargetPath("README.md")));
        Assert.Equal(csBefore, File.ReadAllBytes(fx.TargetPath("lib/a.cs")));
        Assert.Equal(trackerBefore, File.ReadAllBytes(fx.TargetPath("d2net-tracker.json")));
        Assert.Empty(refresh.Summary!.NewlyDiscoveredDartFiles);
    }
}
