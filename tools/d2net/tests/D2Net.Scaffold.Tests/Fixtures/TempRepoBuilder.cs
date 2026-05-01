using System.IO;

namespace D2Net.Scaffold.Tests.Fixtures;

/// <summary>
/// Test-only fixture that creates a fresh temp repo with a fake .git/ and a
/// configurable source directory. Mirrors the D2Net.Init.Tests fixture so
/// the two test projects can share patterns. Fully self-cleaning.
/// </summary>
public sealed class TempRepoBuilder : System.IDisposable
{
    public string Root { get; }
    public string SourceDir { get; }

    public TempRepoBuilder(string sourceName = "glp_runtime")
    {
        Root = Path.Combine(Path.GetTempPath(), "d2net-scaffold-tests", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.Combine(Root, ".git"));
        SourceDir = Path.Combine(Root, sourceName);
        Directory.CreateDirectory(SourceDir);
    }

    public TempRepoBuilder AddDartFile(string relPath, string contents = "// dart")
    {
        var abs = Path.Combine(SourceDir, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        File.WriteAllText(abs, contents);
        return this;
    }

    public TempRepoBuilder AddSourceFile(string relPath, string contents)
    {
        var abs = Path.Combine(SourceDir, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        File.WriteAllText(abs, contents);
        return this;
    }

    public TempRepoBuilder AddDirectory(string relPath)
    {
        var abs = Path.Combine(SourceDir, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(abs);
        return this;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch { /* best effort on Windows file-locking */ }
    }
}
