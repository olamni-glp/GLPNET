using D2Net.Scaffold.Models;

namespace D2Net.Scaffold;

public static class DirectoryWalker
{
    public sealed record WalkResult(
        IReadOnlyList<RelDir> Directories,
        IReadOnlyList<RelFile> Files);

    public static WalkResult Walk(string sourceRoot)
    {
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException($"Source root not found: {sourceRoot}");

        var dirs = new List<RelDir>();
        var files = new List<RelFile>();
        WalkRec(sourceRoot, sourceRoot, dirs, files);

        dirs.Sort((a, b) => string.CompareOrdinal(a.RelPath, b.RelPath));
        files.Sort((a, b) => string.CompareOrdinal(a.RelPath, b.RelPath));

        return new WalkResult(dirs, files);
    }

    private static void WalkRec(
        string root,
        string current,
        List<RelDir> dirs,
        List<RelFile> files)
    {
        foreach (var subDirAbs in Directory.EnumerateDirectories(current))
        {
            var name = Path.GetFileName(subDirAbs);
            if (PrunedDirectories.Names.Contains(name))
                continue;

            var rel = RelPath.Normalize(Path.GetRelativePath(root, subDirAbs));
            dirs.Add(new RelDir(rel, subDirAbs));
            WalkRec(root, subDirAbs, dirs, files);
        }

        foreach (var fileAbs in Directory.EnumerateFiles(current))
        {
            var rel = RelPath.Normalize(Path.GetRelativePath(root, fileAbs));
            files.Add(new RelFile(rel, fileAbs));
        }
    }
}
