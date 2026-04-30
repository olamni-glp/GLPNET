namespace D2Net.Scaffold.Models;

public sealed record RelDir(string RelPath, string AbsSourcePath);

public sealed record RelFile(string RelPath, string AbsSourcePath);

public static class RelPath
{
    public static string Normalize(string path) =>
        path.Replace('\\', '/');

    public static string ToNative(string relPath) =>
        relPath.Replace('/', Path.DirectorySeparatorChar);
}
