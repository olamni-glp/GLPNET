using System.Diagnostics;
using D2Net.Scaffold;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

public class PerfBudgetTests
{
    [Fact]
    public void Scaffold_500DartFiles_2000NonDartFiles_100Dirs_CompletesUnder30Seconds()
    {
        using var fx = new FixtureBuilder();

        var rng = new Random(42);
        // 100 directories, depths 1-4
        var dirs = new List<string>();
        for (var i = 0; i < 100; i++)
        {
            var depth = 1 + (i % 4);
            var parts = Enumerable.Range(0, depth).Select(d => $"d{i}_{d}");
            dirs.Add(string.Join("/", parts));
        }

        // 500 .dart files spread across the dirs
        for (var i = 0; i < 500; i++)
        {
            var dir = dirs[i % dirs.Count];
            fx.AddDartFile($"{dir}/file{i}.dart", $"// dart {i}\n");
        }

        // 2000 non-Dart files
        for (var i = 0; i < 2000; i++)
        {
            var dir = dirs[rng.Next(dirs.Count)];
            fx.AddFile($"{dir}/asset{i}.txt", $"asset {i}\n");
        }

        var sw = Stopwatch.StartNew();
        var outcome = ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));
        sw.Stop();

        Assert.Equal(0, outcome.ExitCode);
        Assert.Equal(500, outcome.Summary!.DartSrcFilesWritten);
        Assert.Equal(2000, outcome.Summary.NonDartFilesCopied);
        Assert.Equal(500 * 9, outcome.Summary.CompanionStubsWritten);
        Assert.True(sw.Elapsed.TotalSeconds < 30,
            $"SC-007: scaffold of 500 dart + 2000 non-dart files must complete in <30s, took {sw.Elapsed.TotalSeconds:0.000}s.");
    }
}
