// SC-003 .NET side: 100 sequential transactions against the unified bridge.
//
// Usage:
//     dotnet run --project specs\012-codeconv-runner\scripts\Sc003NpgsqlLoop -- --port <BRIDGE_PORT> --cycles 100
//
// FR-027: connection string includes `Pooling=false`; no `.Prepare()`.
// Exit codes: 0 = all cycles OK, 1 = at least one failed.

using System;
using System.Diagnostics;
using Npgsql;

internal static class Program
{
    private static int Main(string[] args)
    {
        var host = "127.0.0.1";
        var port = 0;
        var cycles = 100;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host" when i + 1 < args.Length: host = args[++i]; break;
                case "--port" when i + 1 < args.Length: port = int.Parse(args[++i]); break;
                case "--cycles" when i + 1 < args.Length: cycles = int.Parse(args[++i]); break;
            }
        }
        if (port == 0)
        {
            Console.Error.WriteLine("--port is required");
            return 1;
        }

        var connString =
            $"Host={host};Port={port};Database=postgres;Username=postgres;Password=postgres;" +
            $"SSL Mode=Disable;Pooling=false;ApplicationName=sc003-dotnet";

        var errors = new System.Collections.Generic.List<string>();
        var sw = Stopwatch.StartNew();
        try
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();
            using (var setup = new NpgsqlCommand(
                "CREATE TABLE IF NOT EXISTS codeconv._sc003_dotnet (i INT PRIMARY KEY, v TEXT NOT NULL)",
                conn))
            {
                setup.ExecuteNonQuery();
            }
            using (var truncate = new NpgsqlCommand(
                "TRUNCATE codeconv._sc003_dotnet", conn))
            {
                truncate.ExecuteNonQuery();
            }

            for (var i = 0; i < cycles; i++)
            {
                using var tx = conn.BeginTransaction();
                try
                {
                    using (var ins = new NpgsqlCommand(
                        "INSERT INTO codeconv._sc003_dotnet (i, v) VALUES (@i, @v)", conn, tx))
                    {
                        ins.Parameters.AddWithValue("i", i);
                        ins.Parameters.AddWithValue("v", Guid.NewGuid().ToString("N"));
                        ins.ExecuteNonQuery();
                    }
                    using (var sel = new NpgsqlCommand(
                        "SELECT COUNT(*) FROM codeconv._sc003_dotnet WHERE i = @i", conn, tx))
                    {
                        sel.Parameters.AddWithValue("i", i);
                        var got = (long)sel.ExecuteScalar()!;
                        if (got != 1L) throw new InvalidOperationException($"row count {got} != 1");
                    }
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    try { tx.Rollback(); } catch { /* ignore */ }
                    var msg = ex.Message + " | " + ex.GetType().FullName;
                    if (msg.IndexOf("lost synchronization", StringComparison.OrdinalIgnoreCase) >= 0)
                        errors.Add($"cycle {i}: LOST SYNC — {msg}");
                    else if (msg.IndexOf("DuplicatePreparedStatement", StringComparison.OrdinalIgnoreCase) >= 0)
                        errors.Add($"cycle {i}: DUPLICATE PREPARED STATEMENT — {msg}");
                    else
                        errors.Add($"cycle {i}: {msg}");
                }
            }
        }
        catch (Exception fatal)
        {
            Console.Error.WriteLine($"sc003-dotnet: fatal {fatal.Message}");
            return 1;
        }
        sw.Stop();

        if (errors.Count > 0)
        {
            Console.WriteLine($"sc003-dotnet: {errors.Count}/{cycles} failed (elapsed {sw.Elapsed.TotalSeconds:F2}s)");
            foreach (var e in errors.GetRange(0, Math.Min(errors.Count, 5)))
                Console.WriteLine($"  - {e}");
            if (errors.Count > 5)
                Console.WriteLine($"  - ... {errors.Count - 5} more");
            return 1;
        }
        Console.WriteLine(
            $"sc003-dotnet: {cycles}/{cycles} cycles OK in {sw.Elapsed.TotalSeconds:F2}s " +
            "(zero lost-sync, zero duplicate-prepared)");
        return 0;
    }
}
