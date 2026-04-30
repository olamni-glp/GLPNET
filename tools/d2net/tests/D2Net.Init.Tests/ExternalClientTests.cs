using System.Data.Odbc;
using System.IO;
using System.Runtime.InteropServices;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;
using Npgsql;

namespace D2Net.Init.Tests;

/// <summary>
/// SC-004 + SC-005: while a D2NET command (or a test harness with a live
/// bridge) is running, an external Npgsql client AND -- on Windows v1 --
/// an external psqlODBC client can open a session on the persisted port
/// and run the documented operations against the five workspace tables.
///
/// On non-Windows hosts the psqlODBC test early-returns (Q2 clarification:
/// macOS/Linux are best-effort, not release-blocking).
/// </summary>
public class ExternalClientTests
{
    private static (TempRepoBuilder repo, string pgdb) BuildAndInit()
    {
        var repo = new TempRepoBuilder();
        repo.AddDartFile("lib/runtime/runner.dart")
            .AddDartFile("lib/runtime/heap.dart")
            .AddDartFile("lib/foo/bar.dart");
        var port = PortPicker.NextFreePort();
        var so = new StringWriter();
        var se = new StringWriter();
        var code = Program.Run(
            new[] { "--source", "glp_runtime", "--target-extension", "_net",
                    "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                    "--non-interactive", "--bridge-port", port.ToString() },
            new StringReader(""), so, se, repo.Root);
        Assert.Equal(ExitCodes.Success, code);
        var pgdb = Path.Combine(repo.Root, ".D2NET", "pgdb");
        return (repo, pgdb);
    }

    [Fact]
    public void NpgsqlClient_CanRunDocumentedQueries()
    {
        var (repo, pgdb) = BuildAndInit();
        using (repo)
        {
            // DbVerifier already exposes Npgsql; spawning a verifier-side bridge gives us a
            // live wire endpoint for the documented queries.
            using var v = new DbVerifier(pgdb);

            using (var cmd = v.RawConnection.CreateCommand())
            {
                cmd.CommandText = "SELECT 1;";
                Assert.Equal(1, System.Convert.ToInt32(cmd.ExecuteScalar()));
            }
            using (var cmd = v.RawConnection.CreateCommand())
            {
                cmd.CommandText = "SELECT version();";
                var version = (string)cmd.ExecuteScalar()!;
                Assert.Contains("PostgreSQL", version);
                Assert.DoesNotContain("SQLite", version, System.StringComparison.OrdinalIgnoreCase);
            }
            Assert.Equal(3, v.CountRows("dart_files"));
            // phase_status is empty (no rows); a SELECT must succeed.
            using (var cmd = v.RawConnection.CreateCommand())
            {
                cmd.CommandText = "SELECT count(*) FROM phase_status;";
                Assert.Equal(0, System.Convert.ToInt32(cmd.ExecuteScalar()));
            }
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task PsqlOdbcClient_BasicSelectSucceeds_WindowsOnly()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Q2 clarification: macOS / Linux are best-effort, not release-blocking.
            return;
        }
        if (!IsModernPsqlOdbcInstalled())
        {
            // Spec FR-011 hard guarantee is conditioned on the modern installer
            // being present. Document the skip clearly so the test report is honest.
            System.Console.Out.WriteLine(
                "PsqlOdbcClient_BasicSelectSucceeds_WindowsOnly: skipping " +
                "(PostgreSQL ODBC Driver(UNICODE) not installed).");
            return;
        }

        var (repo, pgdb) = BuildAndInit();
        using (repo)
        {
            var port = PortPicker.NextFreePort();
            using var bridge = await PgBridgeProcess.StartAsync(port, pgdb, new StringWriter());
            var bridgeOpts = BridgeOptions.ForDataDir(pgdb, port);
            var odbcString = DbConnectionStringBuilder.BuildOdbc(bridgeOpts);

            using var conn = new OdbcConnection(odbcString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM dart_files";
            // SC-005: returns the correct count, host process still alive (no
            // STATUS_STACK_BUFFER_OVERRUN).
            Assert.Equal(3, System.Convert.ToInt32(cmd.ExecuteScalar()));
        }
    }

    private static bool IsModernPsqlOdbcInstalled()
    {
        // Use OdbcConnection's exception surface to probe driver availability:
        // open with deliberately bad host on a closed port and inspect the
        // exception type. OdbcException = driver loaded; ArgumentException /
        // DllNotFoundException = driver not installed.
        var probe = "Driver={PostgreSQL ODBC Driver(UNICODE)};Server=127.0.0.1;Port=1;Database=d2net;Uid=d2net;Pwd=d2net;SSLmode=disable;";
        try
        {
            using var c = new OdbcConnection(probe);
            c.Open();
            return true; // unlikely
        }
        catch (OdbcException)
        {
            return true; // driver loaded; the actual connect failed (port closed).
        }
        catch
        {
            return false; // driver not installed / unrecognized clause
        }
    }
}
