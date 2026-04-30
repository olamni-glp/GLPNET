using D2Net.Scaffold.Models;

namespace D2Net.Scaffold;

public static class CompanionFileWriter
{
    public static int WriteAllNine(string targetDir, string dartBaseName)
    {
        Directory.CreateDirectory(targetDir);
        var written = 0;
        foreach (var ext in CompanionExtensions.All)
        {
            var path = Path.Combine(targetDir, $"{dartBaseName}.{ext}");
            File.WriteAllText(path, BuildTodoLine(dartBaseName, ext));
            written++;
        }
        return written;
    }

    public static (int Written, bool WasNew) WriteIfMissing(string targetDir, string dartBaseName)
    {
        Directory.CreateDirectory(targetDir);
        var anyExisted = false;
        var anyNew = false;
        var written = 0;
        foreach (var ext in CompanionExtensions.All)
        {
            var path = Path.Combine(targetDir, $"{dartBaseName}.{ext}");
            if (File.Exists(path))
            {
                anyExisted = true;
                continue;
            }

            File.WriteAllText(path, BuildTodoLine(dartBaseName, ext));
            written++;
            anyNew = true;
        }

        // Treat the file group as "newly discovered" only when none of the nine existed beforehand.
        // If some companions existed (e.g. a partial earlier run), don't classify it as a brand-new Dart file.
        var wasNew = anyNew && !anyExisted;
        return (written, wasNew);
    }

    private static string BuildTodoLine(string dartBaseName, string ext) =>
        $"// TODO: d2net — port from {dartBaseName}.dart.src (artifact: {ext}){Environment.NewLine}";
}
