using System.Collections.Generic;
using System.IO;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;
using Npgsql;

namespace D2Net.Init.Tests;

/// <summary>
/// US2 / FR-006: --add-exclude must not modify phase_sequence or
/// phase_status. We seed both tables with non-trivial rows, run the
/// add-exclude mutator, and assert row-by-row equality (including the
/// last_updated timestamp) afterwards.
/// </summary>
public class AddExcludePhaseInvarianceTests
{
    private static (int code, string stdout, string stderr) Run(string[] args, string cwd, string stdin = "")
    {
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(args, new StringReader(stdin), so, se, cwd);
        return (code, so.ToString(), se.ToString());
    }

    [Fact]
    public void PhaseRowsByteIdenticalAfterAddExclude()
    {
        var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runner.dart")
            .AddDartFile("lib/heap.dart")
            .AddDartFile("extra/a.dart")
            .AddDartFile("extra/b.dart");

        var port = PortPicker.NextFreePort();
        using (repo)
        {
            // Init.
            var (icode, _, _) = Run(
                new[] { "--source", "glp_runtime", "--target-extension", "_net",
                        "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                        "--non-interactive", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, icode);

            // Seed phase_sequence and phase_status with non-trivial rows.
            var phaseSeq = new (string Phase, int Sequence)[]
            {
                ("scaffold", 1),
                ("analyze", 2),
                ("port", 3),
            };
            var phaseStatus = new (string Phase, string Status, string FixedTimestamp)[]
            {
                ("scaffold", "COMPLETED", "2026-04-30 10:00:00+00"),
                ("analyze",  "IN_PROGRESS", "2026-04-30 11:30:00+00"),
                ("port",     "PENDING",     "2026-04-30 12:00:00+00"),
            };

            var dataDir = Path.GetFullPath(Path.Combine(repo.Root, ".D2NET", "pgdb"));
            var bridgeOpts = BridgeOptions.ForDataDir(dataDir, port);

            using (var bridge = PgBridgeProcess.StartAsync(port, dataDir, new StringWriter()).GetAwaiter().GetResult())
            using (var conn = new NpgsqlConnection(DbConnectionStringBuilder.BuildNpgsql(bridgeOpts)))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    foreach (var (p, s) in phaseSeq)
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.Transaction = tx;
                        cmd.CommandText = "INSERT INTO phase_sequence (phase, sequence) VALUES (@p, @s);";
                        cmd.Parameters.AddWithValue("@p", p);
                        cmd.Parameters.AddWithValue("@s", s);
                        cmd.ExecuteNonQuery();
                    }
                    foreach (var (p, s, t) in phaseStatus)
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.Transaction = tx;
                        cmd.CommandText = "INSERT INTO phase_status (phase, status, last_updated) VALUES (@p, @s, @t::timestamptz);";
                        cmd.Parameters.AddWithValue("@p", p);
                        cmd.Parameters.AddWithValue("@s", s);
                        cmd.Parameters.AddWithValue("@t", t);
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                conn.Close();
                NpgsqlConnection.ClearAllPools();
            }

            // Snapshot phase tables BEFORE add-exclude.
            var seqBefore = ReadPhaseSequence(dataDir, port);
            var statBefore = ReadPhaseStatus(dataDir, port);

            // Run --add-exclude.
            var (acode, _, ase) = Run(
                new[] { "--add-exclude", "extra", "--bridge-port", port.ToString() },
                repo.Root);
            Assert.Equal(ExitCodes.Success, acode);

            // Snapshot phase tables AFTER add-exclude.
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
