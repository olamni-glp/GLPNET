namespace D2Net.Scaffold.Tests.Fixtures;

public sealed class FixtureBuilder : IDisposable
{
    public string Root { get; }
    public string SourceRoot { get; }
    public string TargetRoot { get; }

    public FixtureBuilder()
    {
        Root = Path.Combine(Path.GetTempPath(), "d2net-scaffold-tests", Guid.NewGuid().ToString("N"));
        SourceRoot = Path.Combine(Root, "source");
        TargetRoot = Path.Combine(Root, "target");
        Directory.CreateDirectory(SourceRoot);
    }

    public FixtureBuilder AddFile(string relPath, string content)
    {
        var abs = Path.Combine(SourceRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        File.WriteAllText(abs, content);
        return this;
    }

    public FixtureBuilder AddBinaryFile(string relPath, byte[] content)
    {
        var abs = Path.Combine(SourceRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        File.WriteAllBytes(abs, content);
        return this;
    }

    public FixtureBuilder AddDartFile(string relPath, string content) =>
        AddFile(relPath, content);

    public FixtureBuilder AddDirectory(string relPath)
    {
        var abs = Path.Combine(SourceRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(abs);
        return this;
    }

    public string SourcePath(string relPath) =>
        Path.Combine(SourceRoot, relPath.Replace('/', Path.DirectorySeparatorChar));

    public string TargetPath(string relPath) =>
        Path.Combine(TargetRoot, relPath.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
