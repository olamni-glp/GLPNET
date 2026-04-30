using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace D2Net.Init.Inspectors;

internal sealed class DartFileRow
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("filename")] public string Filename { get; set; } = "";
    [JsonPropertyName("full_path")] public string FullPath { get; set; } = "";
}

internal sealed class ListInspectorResult
{
    [JsonPropertyName("dart_files")] public List<DartFileRow> DartFiles { get; set; } = new();
}

public static class ListInspector
{
    public static void Run(SqliteConnection conn, bool json, TextWriter stdout)
    {
        var rows = new List<DartFileRow>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, filename, full_path FROM dart_files ORDER BY full_path ASC;";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                rows.Add(new DartFileRow
                {
                    Id = rdr.GetInt64(0),
                    Filename = rdr.GetString(1),
                    FullPath = rdr.GetString(2),
                });
            }
        }
        if (json)
        {
            OutputFormat.WriteJson(stdout, new ListInspectorResult { DartFiles = rows });
        }
        else
        {
            foreach (var r in rows) OutputFormat.WriteTsvLine(stdout, r.Filename, r.FullPath);
        }
    }
}
