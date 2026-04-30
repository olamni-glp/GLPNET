namespace D2Net.Scaffold.Models;

public static class PrunedDirectories
{
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.Ordinal)
    {
        ".dart_tool",
        "build",
        ".git",
        ".idea",
        ".vscode",
    };
}
