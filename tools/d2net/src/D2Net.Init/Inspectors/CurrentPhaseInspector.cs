using System.IO;
using System.Text.Json.Serialization;
using Npgsql;

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
    public static void Run(NpgsqlConnection conn, bool json, TextWriter stdout)
    {
        // FR-019: lowest-sequence row in phase_status whose status != 'COMPLETED'.
        // last_updated is TIMESTAMPTZ in PGLite; render as ISO-8601 UTC with trailing 'Z'
        // to preserve the shipped 002 wire format.
        const string sql = @"
            SELECT s.phase, s.status,
                   to_char(s.last_updated AT TIME ZONE 'UTC', 'YYYY-MM-DD""T""HH24:MI:SS""Z""'),
                   q.sequence
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
            var ts = rdr.GetString(2); // ISO-8601 UTC formatted in SQL.
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
