using System.IO;
using System.Text.RegularExpressions;

namespace D2Net.Scaffold.Tests;

/// <summary>
/// T034: static SQL audit. Scan ScaffoldRunner.cs and ScaffoldDbWriter.cs
/// for any SQL referencing phase_sequence or phase_status outside the
/// approved scaffold-row UPSERT / UPDATE statements. The approved touch
/// points are:
///   - <c>INSERT INTO phase_status (phase, status, ...) VALUES ('scaffold', 'IN_PROGRESS', ...) ON CONFLICT ...</c>
///   - <c>UPDATE phase_status SET status='COMPLETED', last_updated=now() WHERE phase='scaffold'</c>
///   - <c>INSERT INTO phase_sequence (phase, sequence) SELECT 'scaffold', ...</c>
/// Any other phase reference fails the audit.
/// </summary>
public class ScaffoldSqlAuditTests
{
    [Fact]
    public void NoUnapprovedPhaseTableTouches()
    {
        // Walk up from the test assembly to locate the source files.
        var asmDir = Path.GetDirectoryName(typeof(ScaffoldSqlAuditTests).Assembly.Location)!;
        // bin/Debug/net8.0 -> ../../../../src/D2Net.Scaffold/
        var srcRoot = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", "src", "D2Net.Scaffold"));
        Assert.True(Directory.Exists(srcRoot), $"src dir not found at {srcRoot}");

        var runner = File.ReadAllText(Path.Combine(srcRoot, "ScaffoldRunner.cs"));
        var dbWriter = File.ReadAllText(Path.Combine(srcRoot, "ScaffoldDbWriter.cs"));

        // ScaffoldRunner.cs must NOT issue any SQL touching phase_status or phase_sequence
        // (delegated entirely to ScaffoldDbWriter.cs). XML-doc / comment mentions are OK,
        // but no SQL keyword paired with these tables.
        AssertNoSqlTouching(runner, "phase_status");
        AssertNoSqlTouching(runner, "phase_sequence");

        // ScaffoldDbWriter.cs may touch phase_status / phase_sequence, but every
        // SQL touching them must be paired with the scaffold phase identifier.
        AssertEveryPhaseReferenceMentionsScaffold(dbWriter, "phase_status");
        AssertEveryPhaseReferenceMentionsScaffold(dbWriter, "phase_sequence");
    }

    private static void AssertEveryPhaseReferenceMentionsScaffold(string source, string table)
    {
        var idx = 0;
        while (true)
        {
            var pos = source.IndexOf(table, idx, System.StringComparison.Ordinal);
            if (pos < 0) break;
            // Look at a 400-char window around the reference; assert 'scaffold' appears.
            var start = System.Math.Max(0, pos - 200);
            var end = System.Math.Min(source.Length, pos + 200);
            var window = source.Substring(start, end - start);
            Assert.Contains("'scaffold'", window);
            idx = pos + table.Length;
        }
    }

    private static void AssertNoSqlTouching(string source, string table)
    {
        // Inspect non-comment lines only. Comments (// and ///) may mention the table conceptually.
        // A non-comment line containing both an SQL keyword and the table name is a violation:
        // phase tables must be touched only via ScaffoldDbWriter.
        var sqlKeywords = new[] { "INSERT INTO", "UPDATE ", "DELETE FROM", "SELECT", "ALTER TABLE" };
        foreach (var line in source.Split('\n'))
        {
            if (!line.Contains(table, System.StringComparison.Ordinal)) continue;
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//", System.StringComparison.Ordinal)) continue;
            foreach (var kw in sqlKeywords)
            {
                if (line.Contains(kw, System.StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail($"ScaffoldRunner.cs contains SQL '{kw.Trim()} ... {table}': {line.Trim()}");
                }
            }
        }
    }
}
