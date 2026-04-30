using System.Linq;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

public class DartFileScannerTests
{
    [Fact]
    public void ScansDartFilesAndUsesForwardSlashes()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runtime/runner.dart")
            .AddDartFile("lib/runtime/heap.dart")
            .AddDartFile("test/widget/sample.dart")
            .AddDartFile("not_a_dart.txt");

        var entries = DartFileScanner.Scan(repo.Root, "glp_runtime", Array.Empty<string>());

        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.DoesNotContain("\\", e.FullPath));
        Assert.All(entries, e => Assert.StartsWith("glp_runtime/", e.FullPath));
        Assert.Contains(entries, e => e.Filename == "runner.dart" && e.FullPath == "glp_runtime/lib/runtime/runner.dart");
    }

    [Fact]
    public void SkipsExcludedDirectories()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/active.dart")
            .AddDartFile("archive_2024/old.dart")
            .AddDartFile(".git/internal.dart");

        var entries = DartFileScanner.Scan(
            repo.Root,
            "glp_runtime",
            new[] { ".git", "archive_2024" });

        Assert.Single(entries);
        Assert.Equal("active.dart", entries[0].Filename);
    }

    [Fact]
    public void ResultsAreSortedByFullPathAscending()
    {
        using var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/zzz.dart")
            .AddDartFile("aaa/aaa.dart")
            .AddDartFile("lib/aaa.dart");

        var entries = DartFileScanner.Scan(repo.Root, "glp_runtime", Array.Empty<string>());

        var paths = entries.Select(e => e.FullPath).ToArray();
        var sorted = paths.OrderBy(p => p, StringComparer.Ordinal).ToArray();
        Assert.Equal(sorted, paths);
    }

    [Fact]
    public void EmptySourceProducesEmptyList()
    {
        using var repo = new TempRepoBuilder();
        var entries = DartFileScanner.Scan(repo.Root, "glp_runtime", Array.Empty<string>());
        Assert.Empty(entries);
    }
}
