using System.Collections.Generic;
using System.IO;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;
using Npgsql;

namespace D2Net.Init.Tests;

/// <summary>
/// US2 / FR-007: --remove-exclude must not modify phase_sequence or
/// phase_status. Mirrors feature 007's invariance test.
/// </summary>
public class RemoveExcludePhaseInvarianceTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    [Fact]
    public void PhaseRowsByteIdenticalAfterRemoveExclude()
    {
        var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart")
            .AddDartFile("extra/a.dart")
            .AddDartFile("extra/b.dart");

        var port = PortPicker.NextFreePort();
        using (repo)
        {
            var (icode, _, _) = Run(
                new[] { "--source", "glp_runtime", "--target-extension", "_net",
                        "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                        "--non-interactive", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, icode);

            // Add `extra` as manual exclusion.
            var (acode, _, _) = Run(
                new[] { "--add-exclude", "extra", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, acode);

            // Seed phase tables.
            var phaseStatus = new (string Phase, string Status, string Ts)[]
            {
                ("scaffold", "COMPLETED", "2026-04-30 10:00:00+00"),
                ("analyze",  "IN_PROGRESS", "2026-04-30 11:30:00+00"),
                ("port",     "PENDING",     "2026-04-30 12:00:00+00"),
            };
            var phaseSeq = new (string Phase, int Sequence)[]
            {
                ("scaffold", 1), ("analyze", 2), ("port", 3),
            };

            var dataDir = Path.GetFullPath(Path.Combine(repo.Root, ".D2NET", "pgdb"));
            var bridgeOpts = BridgeOptions.ForDataDir(dataDir, port);

            using (var bridge = PgBridgeProcess.StartAsync(port, dataDir, new StringWriter()).GetAwaiter().GetResult())
            using (var conn = new NpgsqlConnection(DbConnectionStringBuilder.BuildNpgsql(bridgeOpts)))
            {
                conn.Open();
                using var tx = conn.BeginTransaction();
                foreach (var (p, s) in phaseSeq)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO phase_sequence (phase, sequence) VALUES (@p, @s);";
                    cmd.Parameters.AddWithValue("@p", p);
                    cmd.Parameters.AddWithValue("@s", s);
                    cmd.ExecuteNonQuery();
                }
                foreach (var (p, s, ts) in phaseStatus)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO phase_status (phase, status, last_updated) VALUES (@p, @s, @t::timestamptz);";
                    cmd.Parameters.AddWithValue("@p", p);
                    cmd.Parameters.AddWithValue("@s", s);
                    cmd.Parameters.AddWithValue("@t", ts);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
                conn.Close();
                NpgsqlConnection.ClearAllPools();
            }

            var seqBefore = ReadPhaseSequence(dataDir, port);
            var statBefore = ReadPhaseStatus(dataDir, port);

            // Run --remove-exclude.
            var (rcode, _, _) = Run(
                new[] { "--remove-exclude", "extra", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, rcode);

            var seqAfter = ReadPhaseSequence(dataDir, port);
            var statAfter = ReadPhaseStatus(dataDir, port);

            Assert.Equal(seqBefore, seqAfter);
            Assert.Equal(statBefore, statAfter);
        }
    }

    private static List<(string, int)> ReadPhaseSequence(string dataDir, int port)
    {
        var bridgeOpts = BridgeOptions.ForDataDir(dataDir, port);
        var rows = new List<(string, int)>();
        using var bridge = PgBridgeProcess.StartAsync(port, dataDir, new StringWriter()).GetAwaiter().GetResult();
        using var conn = new NpgsqlConnection(DbConnectionStringBuilder.BuildNpgsql(bridgeOpts));
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT phase, sequence FROM phase_sequence ORDER BY phase;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) rows.Add((r.GetString(0), r.GetInt32(1)));
        }
        conn.Close();
        NpgsqlConnection.ClearAllPools();
        return rows;
    }

    private static List<(string, string, string)> ReadPhaseStatus(string dataDir, int port)
    {
        var bridgeOpts = BridgeOptions.ForDataDir(dataDir, port);
        var rows = new List<(string, string, string)>();
        using var bridge = PgBridgeProcess.StartAsync(port, dataDir, new StringWriter()).GetAwaiter().GetResult();
        using var conn = new NpgsqlConnection(DbConnectionStringBuilder.BuildNpgsql(bridgeOpts));
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT phase, status, " +
                "to_char(last_updated AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"') " +
                "FROM phase_status ORDER BY phase;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) rows.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
        }
        conn.Close();
        NpgsqlConnection.ClearAllPools();
        return rows;
    }
}
