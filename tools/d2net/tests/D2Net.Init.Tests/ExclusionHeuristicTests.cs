using D2Net.Init;

namespace D2Net.Init.Tests;

/// <summary>SC-010: ≥95% correct classification on a curated 20-name fixture.</summary>
public class ExclusionHeuristicTests
{
    [Theory]
    [InlineData("archive_2024", true)]
    [InlineData("backup", true)]
    [InlineData("backups", true)]
    [InlineData("bak", true)]
    [InlineData("old", true)]
    [InlineData("legacy_lib", true)]
    [InlineData("obsolete-modules", true)]
    [InlineData("deprecated", true)]
    [InlineData("attic", true)]
    [InlineData("old_experiments", true)]
    [InlineData("2023_archives", true)]
    [InlineData("backup-temp", true)]
    [InlineData("lib", false)]
    [InlineData("runtime", false)]
    [InlineData("compiler", false)]
    [InlineData("test", false)]
    [InlineData("src", false)]
    [InlineData("services", false)]
    [InlineData("models", false)]
    [InlineData("widgets", false)]
    public void HeuristicMatches(string leafName, bool expectedMatch)
    {
        var actual = ExclusionDetector.TryMatchArchiveMarker(leafName, out _);
        Assert.Equal(expectedMatch, actual);
    }

    [Theory]
    [InlineData(".git", true)]
    [InlineData(".dart_tool", true)]
    [InlineData("build", true)]
    [InlineData(".idea", true)]
    [InlineData(".vscode", true)]
    [InlineData("node_modules", true)]
    [InlineData("lib", false)]
    [InlineData(".github", false)]
    public void WellKnownToolDirsMatch(string leafName, bool expected)
    {
        Assert.Equal(expected, ExclusionDetector.IsWellKnownToolDir(leafName));
    }
}
