namespace D2Net.Scaffold;

public static class FileCopier
{
    public static void CopyVerbatim(string absSource, string absTarget)
    {
        EnsureParentDir(absTarget);
        File.Copy(absSource, absTarget, overwrite: true);
    }

    public static void CopyAsDartSrc(string absSource, string absTargetDartSrc)
    {
        EnsureParentDir(absTargetDartSrc);
        File.Copy(absSource, absTargetDartSrc, overwrite: true);
    }

    private static void EnsureParentDir(string absTarget)
    {
        var dir = Path.GetDirectoryName(absTarget);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
}
