using D2Net.Scaffold.Models;

namespace D2Net.Scaffold.Tests;

public class RelPathNormalizationTests
{
    [Fact]
    public void Normalize_ConvertsBackslashesToForwardSlashes()
    {
        Assert.Equal("lib/runner.dart", RelPath.Normalize("lib\\runner.dart"));
        Assert.Equal("a/b/c.txt", RelPath.Normalize("a\\b\\c.txt"));
    }

    [Fact]
    public void Normalize_LeavesAlreadyNormalizedPaths_Unchanged()
    {
        Assert.Equal("lib/runner.dart", RelPath.Normalize("lib/runner.dart"));
    }

    [Fact]
    public void ToNative_ConvertsForwardSlashesToHostSeparator()
    {
        var result = RelPath.ToNative("a/b/c.txt");
        Assert.Equal($"a{Path.DirectorySeparatorChar}b{Path.DirectorySeparatorChar}c.txt", result);
    }
}
