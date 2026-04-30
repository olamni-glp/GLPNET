using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace D2Net.Init.Inspectors;

internal sealed class ExclusionsInspectorResult
{
    [JsonPropertyName("excluded_directories")] public List<string> ExcludedDirectories { get; set; } = new();
}

public static class ExclusionsInspector
{
    public static void Run(SqliteConnection conn, bool json, TextWriter stdout)
    {
        var rows = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT path FROM excluded_directories ORDER BY path ASC;";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read()) rows.Add(rdr.GetString(0));
        }
        if (json)
        {
            OutputFormat.WriteJson(stdout, new ExclusionsInspectorResult { ExcludedDirectories = rows });
        }
        else
        {
            foreach (var p in rows) stdout.WriteLine(p);
        }
    }
}
