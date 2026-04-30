using System.IO;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace D2Net.Init.Inspectors;

internal sealed class CurrentPhaseRow
{
    [JsonPropertyName("phase")] public string? Phase { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("last_updated")] public string? LastUpdated { get; set; }
    [JsonPropertyName("sequence")] public int? Sequence { get; set; }
}

public static class CurrentPhaseInspector
{
    public static void Run(SqliteConnection conn, bool json, TextWriter stdout)
    {
        // FR-019: lowest-sequence row in phase_status whose status != 'COMPLETED'.
        const string sql = @"
            SELECT s.phase, s.status, s.last_updated, q.sequence
            FROM phase_status s
            JOIN phase_sequence q ON q.phase = s.phase
            WHERE s.status <> 'COMPLETED'
            ORDER BY q.sequence ASC
            LIMIT 1;";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
        {
            var phase = rdr.GetString(0);
            var status = rdr.GetString(1);
            var ts = rdr.GetString(2); // already ISO-8601 UTC text
            var seq = rdr.GetInt32(3);
            if (json)
            {
                OutputFormat.WriteJson(stdout, new CurrentPhaseRow
                {
                    Phase = phase,
                    Status = status,
                    LastUpdated = ts,
                    Sequence = seq,
                });
            }
            else
            {
                OutputFormat.WriteTsvLine(stdout, phase, status, $"last_updated={ts}");
            }
        }
        else
        {
            if (json)
            {
                OutputFormat.WriteJson(stdout, new CurrentPhaseRow { Phase = null });
            }
            else
            {
                stdout.WriteLine("no active phase");
            }
        }
    }
}
