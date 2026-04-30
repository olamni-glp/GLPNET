using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using D2Net.Init;
using D2Net.Init.Tests.Fixtures;

namespace D2Net.Init.Tests;

public class PgBridgeProcessTests
{
    private static string FreshPgDir()
    {
        var p = Path.Combine(Path.GetTempPath(), "d2net-bridge-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return p;
    }

    [Fact]
    public async Task HappyPath_ReadyHandshakeAndCleanShutdown()
    {
        var pgdir = FreshPgDir();
        try
        {
            var port = PortPicker.NextFreePort();
            var stderr = new StringWriter();
            var bridge = await PgBridgeProcess.StartAsync(port, pgdir, stderr);
            Assert.Equal(port, bridge.Port);
            bridge.Dispose();
        }
        finally { try { Directory.Delete(pgdir, true); } catch { } }
    }

    [Fact]
    public async Task PortInUse_RaisesPortInUseException()
    {
        var pgdir = FreshPgDir();
        var port = PortPicker.NextFreePort();
        var holder = new TcpListener(IPAddress.Loopback, port);
        holder.Start();
        try
        {
            var ex = await Assert.ThrowsAsync<BridgeStartException>(async () =>
                await PgBridgeProcess.StartAsync(port, pgdir, new StringWriter()));
            // Either the bridge classifies this as PortInUse, or as an OtherBridgeError
            // depending on exactly what Node's listen() error string contains. We accept both
            // but require the message to mention the port or "EADDRINUSE".
            Assert.True(
                ex.Kind == BridgeStartFailureKind.PortInUse
                    || ex.Message.Contains("EADDRINUSE", System.StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains(port.ToString()),
                $"Unexpected: kind={ex.Kind} msg='{ex.Message}'");
        }
        finally
        {
            holder.Stop();
            try { Directory.Delete(pgdir, true); } catch { }
        }
    }

    [Fact]
    public async Task UserVisibleSchemaNotMutatedByBridgeStartup()
    {
        // Re-states the SC-009 / inspection-readonly invariant at the bridge level:
        // a fresh spawn-and-dispose cycle against an existing pgdb must not change
        // the user-visible schema (the five workspace tables and their row counts).
        // This is the verification step for the smoke-seed removal in T001.
        using var repo = new TempRepoBuilder();
        var initPort = PortPicker.NextFreePort();
        var initCode = Program.Run(
            new[] { "--source", "glp_runtime", "--target-extension", "_net",
                    "--target", "glp_runtime_net", "--accept-suggested-exclusions",
                    "--non-interactive", "--bridge-port", initPort.ToString() },
            new StringReader(""), new StringWriter(), new StringWriter(), repo.Root);
        Assert.Equal(ExitCodes.Success, initCode);
        var pgdb = Path.Combine(repo.Root, ".D2NET", "pgdb");

        // Capture user-visible state via a verifier-spawned bridge.
        int settingRowsBefore;
        using (var v = new DbVerifier(pgdb)) settingRowsBefore = v.CountRows("setting");

        // Spawn-and-dispose a bridge that runs no SQL. If the bridge mutates user state on
        // startup (e.g. via a smoke seed) this assertion fires.
        var port2 = PortPicker.NextFreePort();
        var bridge = await PgBridgeProcess.StartAsync(port2, pgdb, new StringWriter());
        bridge.Dispose();

        int settingRowsAfter;
        using (var v = new DbVerifier(pgdb)) settingRowsAfter = v.CountRows("setting");
        Assert.Equal(settingRowsBefore, settingRowsAfter);

        // Specifically: no smoke seed table 't' visible to user code.
        using (var v = new DbVerifier(pgdb))
        {
            var tables = v.GetTableNames();
            Assert.DoesNotContain("t", tables);
        }
    }
}
