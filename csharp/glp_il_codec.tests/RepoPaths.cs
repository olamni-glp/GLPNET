namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// Locates repo-relative paths from the test assembly, so the harness runs from
/// any working directory. Walks up from the test binary until it finds the marker
/// <c>programs/self.glp</c> (the root prelude).
/// </summary>
internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    /// <summary>Absolute path to the root prelude required by <c>GlpEngine</c>.</summary>
    public static string RootSelfGlp => Path.Combine(Root, "programs", "self.glp");

    /// <summary>Absolute path to a named source under <c>programs/</c>.</summary>
    public static string Program(params string[] relative) =>
        Path.Combine(new[] { Root, "programs" }.Concat(relative).ToArray());

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "programs", "self.glp")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate repo root (no ancestor contains programs/self.glp)");
    }
}
