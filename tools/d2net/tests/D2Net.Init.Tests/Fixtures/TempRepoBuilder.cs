using System.IO;

namespace D2Net.Init.Tests.Fixtures;

public sealed class TempRepoBuilder : IDisposable
{
    public string Root { get; }
    public string SourceDir { get; }

    public TempRepoBuilder(string sourceName = "glp_runtime")
    {
        Root = Path.Combine(Path.GetTempPath(), "d2net-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        // Create a fake .git/ so the WorkspaceLayout repo-root check passes.
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
