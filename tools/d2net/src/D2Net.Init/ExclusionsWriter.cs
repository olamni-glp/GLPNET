using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace D2Net.Init;

public static class ExclusionsWriter
{
    public static void WriteRows(SqliteConnection conn, IReadOnlyList<ProposedExclusion> approved)
    {
        foreach (var ex in approved)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO excluded_directories (path, kind) VALUES ($path, $kind);";
            cmd.Parameters.AddWithValue("$path", ex.Path);
            cmd.Parameters.AddWithValue("$kind", ToKindText(ex.Kind));
            cmd.ExecuteNonQuery();
        }
    }

    public static string ToKindText(ExclusionKind k) => k switch
    {
        ExclusionKind.Tool => "tool",
        ExclusionKind.Pattern => "pattern",
        ExclusionKind.Manual => "manual",
        _ => "manual",
    };
}
