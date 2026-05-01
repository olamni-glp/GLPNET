namespace D2Net.Scaffold.Models;

/// <summary>
/// Path-canonicalisation helpers for scaffold's source-relative work.
/// Forward-slashes are the on-the-wire form (mirrors <c>dart_files.full_path</c>).
/// </summary>
public static class RelPath
{
    public static string Normalize(string path) =>
        path.Replace('\\', '/');

    public static string ToNative(string relPath) =>
        relPath.Replace('/', System.IO.Path.DirectorySeparatorChar);
}
