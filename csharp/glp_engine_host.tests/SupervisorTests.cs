// T029 — supervisor tests (US3): kill→detect→restart within the ping budget
// (US3 independent test), backoff progression, DEF-F2 taxonomy stop
// (restart-storm edge case), crash-record completeness (FR-024), and the
// corrupt-latest previous-seq fallback.
//
// These run the REAL engine binary (built by the project reference) as the
// supervised child — process-level supervision is the thing under test, so an
// in-proc fake would prove nothing about it. Fast supervision knobs keep each
// test inside a few seconds.

using System.Diagnostics;

using GlpRuntime.Engine;
using GlpRuntime.EngineHost;
using GlpRuntime.EngineHost.Snapshot;
using GlpRuntime.EngineHost.Store;
using GlpRuntime.Supervisor;

using SupervisorService = GlpRuntime.Supervisor.Supervisor;

namespace GlpRuntime.EngineHost.Tests;

public class SupervisorTests : IDisposable
{
    private readonly string _storeRoot =
        Path.Combine(Path.GetTempPath(), $"glpsnap-t029-{Guid.NewGuid():N}");
    private readonly string _rootSelfGlp = GlpRuntime.EngineHost.Program.ResolveRootSelfGlpPath();

    public void Dispose()
    {
        try { Directory.Delete(_storeRoot, recursive: true); } catch (IOException) { }
    }

    /// <summary>The built engine host binary (beside the test assembly's repo checkout).</summary>
    private static string EngineBinary()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot != null && !File.Exists(Path.Combine(repoRoot.FullName, "programs", "self.glp")))
            repoRoot = repoRoot.Parent;
        Assert.NotNull(repoRoot);
        var exe = Path.Combine(repoRoot!.FullName,
            "csharp", "glp_engine_host", "bin", "Debug", "net10.0", "glp_engine_host.exe");
        Assert.True(File.Exists(exe), $"engine binary not built: {exe}");
        return exe;
    }

    private SupervisorConfig FastConfig(int port, string engineBinary, int crashThreshold = 3) => new()
    {
        EngineBinary = engineBinary,
        Listen = $"127.0.0.1:{port}",
        StoreRoot = _storeRoot,
        PingInterval = TimeSpan.FromMilliseconds(200),
        PingTimeout = TimeSpan.FromMilliseconds(800),
        StartupBudget = TimeSpan.FromSeconds(30),
        BackoffInitial = TimeSpan.FromMilliseconds(50),
        BackoffMultiplier = 2.0,
        BackoffMax = TimeSpan.FromSeconds(1),
        CrashWindow = TimeSpan.FromMinutes(1),
        CrashThreshold = crashThreshold,
    };

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        int port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string what)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(100);
        }
        Assert.Fail($"timed out waiting for: {what}");
    }

    /// <summary>Seed the file store with one real (empty-engine) snapshot at seq 1.</summary>
    private void SeedSnapshot(int port, ulong seq = 1)
    {
        var engine = new GlpEngine(_rootSelfGlp);
        var quiescence = new Quiescence(engine);
        var disarmed = quiescence.DisarmTimersForCapture()!;
        var blob = SnapshotCapture.Capture(
            engine, null, Array.Empty<LoadedUnit>(), File.ReadAllText(_rootSelfGlp),
            disarmed, $"engine-{port}", seq);
        new FileSnapshotStore(_storeRoot, $"engine-{port}").Write(
            seq, blob.CreatedUtcMs, blob.FormatVersion, blob.Encode());
    }

    // ------------------------------------------------- kill → detect → restart

    [Fact]
    public async Task KillEngine_SupervisorDetects_RestartsFromLatestSnapshot()
    {
        var port = FreePort();
        SeedSnapshot(port);
        using var supervisor = new SupervisorService(FastConfig(port, EngineBinary()));
        using var cts = new CancellationTokenSource();
        try
        {
            await supervisor.StartAsync(cts.Token);
            await WaitUntilAsync(
                () => supervisor.Log.ReadStatus()?.EngineState == "healthy",
                TimeSpan.FromSeconds(30), "first healthy engine");
            var firstPid = supervisor.EnginePid;
            Assert.NotNull(firstPid);
            Assert.Equal(1UL, supervisor.Log.ReadStatus()!.LastSnapshotSeq); // initial start restored seq 1

            // Kill the engine PID externally (US3 independent test).
            Process.GetProcessById(firstPid!.Value).Kill(entireProcessTree: true);

            // Detection within the ping budget, replacement restored + healthy (AS-2/AS-3).
            await WaitUntilAsync(
                () => supervisor.Log.History().Count > 0 &&
                      supervisor.Log.ReadStatus()?.EngineState == "healthy" &&
                      supervisor.EnginePid is int pid && pid != firstPid,
                TimeSpan.FromSeconds(30), "crash recorded + replacement healthy");

            // Crash-record completeness (FR-024, data-model.md CrashRecord;
            // contracts/supervision.md steps 1+4): a DURABLE detection record lands
            // BEFORE backoff/restart, then the completion record when the
            // replacement serves.
            var history = supervisor.Log.History();
            Assert.Equal(2, history.Count);
            var detected = history[0];
            Assert.Equal($"engine-{port}", detected.EngineIdentity);
            Assert.Equal("restarting", detected.RestartOutcome); // step 1: durable at detection
            Assert.True(detected.Detection is CrashDetection.Exit or CrashDetection.PingTimeout);
            var record = history[1];
            Assert.Equal($"engine-{port}", record.EngineIdentity);
            Assert.True(record.TimestampUtc > DateTimeOffset.UtcNow.AddMinutes(-5));
            Assert.Equal("restored(1)", record.RestartOutcome); // AS-3: from the latest snapshot
            Assert.True(record.BackoffAppliedMs >= 50);
            Assert.True(record.Detection is CrashDetection.Exit or CrashDetection.PingTimeout);
        }
        finally
        {
            cts.Cancel();
            try { await supervisor.StopAsync(CancellationToken.None); }
            catch (OperationCanceledException) { }
        }
    }

    // -------------------------------------- backoff progression + taxonomy stop

    [Fact]
    public async Task InstantlyDyingChild_BackoffProgresses_ThenTaxonomyStopsTheLoop()
    {
        var port = FreePort();
        // A child that dies immediately every time (restart-storm edge case):
        // the test OCCUPIES the engine's port, so every engine start hits the
        // port-in-use loud refusal and exits at once — deterministic, and it
        // exercises the real binary's real failure path.
        var squatter = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
        squatter.Start();
        var config = FastConfig(port, EngineBinary(), crashThreshold: 3) with
        {
            StartupBudget = TimeSpan.FromSeconds(5),
        };
        using var supervisor = new SupervisorService(config);
        using var cts = new CancellationTokenSource();
        try
        {
            await supervisor.StartAsync(cts.Token);
            await WaitUntilAsync(
                () => supervisor.StoppedReason is not null,
                TimeSpan.FromSeconds(30), "unrecoverable classification");

            // DEF-F2: repeated_immediate_crash stops the loop (FR-023), loudly
            // classified and persisted on the record.
            Assert.Equal("repeated_immediate_crash", supervisor.StoppedReason);
            var last = supervisor.Log.History().Last();
            Assert.Equal("unrecoverable(repeated_immediate_crash)", last.RestartOutcome);
            Assert.Contains("stopped(repeated_immediate_crash)",
                supervisor.Log.ReadStatus()!.EngineState);

            // Backoff progressed geometrically before the stop (initial × multiplier).
            var backoffs = supervisor.BackoffHistoryMs;
            Assert.True(backoffs.Count >= 2, $"expected ≥2 applied backoffs, saw {backoffs.Count}");
            Assert.Equal(50, backoffs[0]);
            Assert.Equal(100, backoffs[1]);
        }
        finally
        {
            squatter.Stop();
            cts.Cancel();
            try { await supervisor.StopAsync(CancellationToken.None); }
            catch (OperationCanceledException) { }
        }
    }

    // -------------------------------------------- corrupt-latest fallback (once)

    [Fact]
    public async Task CorruptLatestSnapshot_FallsBackToPreviousSeq_Once()
    {
        var port = FreePort();
        SeedSnapshot(port, seq: 1);                     // good previous
        new FileSnapshotStore(_storeRoot, $"engine-{port}").Write(
            2, 1_000, 1, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }); // corrupt latest

        using var supervisor = new SupervisorService(FastConfig(port, EngineBinary()));
        using var cts = new CancellationTokenSource();
        try
        {
            await supervisor.StartAsync(cts.Token);
            await WaitUntilAsync(
                () => supervisor.Log.ReadStatus()?.EngineState == "healthy",
                TimeSpan.FromSeconds(30), "healthy engine after previous-seq fallback");

            // The engine serves from seq 1 — the DEF-F2 "fall back once" path.
            Assert.Equal(1UL, supervisor.Log.ReadStatus()!.LastSnapshotSeq);
            Assert.Null(supervisor.StoppedReason);
        }
        finally
        {
            cts.Cancel();
            try { await supervisor.StopAsync(CancellationToken.None); }
            catch (OperationCanceledException) { }
        }
    }
}
