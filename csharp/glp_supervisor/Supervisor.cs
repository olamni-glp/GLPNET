// Supervisor — BackgroundService hosting the engine child process (T025;
// contracts/supervision.md, FR-020/022/023).
//
// Topology (MVP): the supervisor is the engine's ONE wire client (FR-002) — it
// holds the client slot and pings over it (wire rule 7). Liveness is
// HOST-TIMER ONLY (FR-021/DEF-F1: the self-prove GLP goal is proposal-only
// this wave — see docs/research/repl-engine-separation/self-prove-liveness-proposal.md).
//
// Death = child-process exit OR a ping with no ACK inside PingTimeout (one
// fresh-connection retry is folded into that budget, so a broken socket with a
// live engine is not misread as death). On death: record → backoff → restart
// via the restore path (--from-snapshot latest; previous-seq fallback ONCE on
// a corrupt latest) → first healthy ping completes the CrashRecord with
// restored(seq). The DEF-F2 taxonomy stops the loop loudly (FR-023).

using System.Diagnostics;

using GlpRuntime.EngineHost.Store;
using GlpRuntime.ReplClient;
using GlpRuntime.SplitProtocol;

using Microsoft.Extensions.Hosting;

namespace GlpRuntime.Supervisor;

public sealed class Supervisor : BackgroundService
{
    private readonly SupervisorConfig _config;
    private readonly CrashLog _crashLog;
    private readonly string _host;
    private readonly int _port;

    private Process? _child;
    private readonly List<string> _recentStderr = new();
    private readonly List<DateTimeOffset> _crashTimes = new();
    private TimeSpan _backoff;
    private DateTimeOffset? _lastHeartbeat;
    private ulong? _lastRestoredSeq;

    /// <summary>Set when the loop stopped on an unrecoverable classification (FR-023).</summary>
    public string? StoppedReason { get; private set; }

    /// <summary>Applied backoff delays in order (test observability for the progression).</summary>
    internal IReadOnlyList<double> BackoffHistoryMs => _backoffHistory;
    private readonly List<double> _backoffHistory = new();

    /// <summary>The current engine child's PID (tests kill it externally).</summary>
    public int? EnginePid => _child is { HasExited: false } ? _child.Id : null;

    public CrashLog Log => _crashLog;

    public Supervisor(SupervisorConfig config)
    {
        _config = config;
        _crashLog = new CrashLog(config.StoreRoot);
        _backoff = config.BackoffInitial; // first crash backs off from INITIAL, not zero
        var idx = config.Listen.LastIndexOf(':');
        _host = config.Listen[..idx];
        _port = int.Parse(config.Listen[(idx + 1)..]);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Initial start: resume from the latest snapshot when one exists — an
        // unattended service coming back up should not silently lose state.
        // (The contract constrains REPLACEMENT starts to the restore path; the
        // initial start follows the same rule for the same reason.)
        double backoffAppliedMs = 0;
        CrashDetection detection = CrashDetection.Exit;
        int? exitCode = null;
        bool firstStart = true;

        while (!ct.IsCancellationRequested)
        {
            // ---- start (or restart) the engine via the restore path ----
            ulong? restoredSeq;
            try
            {
                restoredSeq = await StartEngineWithRestoreFallbackAsync(ct).ConfigureAwait(false);
            }
            catch (UnrecoverableException ex)
            {
                Stop(ex.Reason, firstStart
                    ? null
                    : (detection, exitCode, backoffAppliedMs));
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // ---- first healthy ping confirms the (re)start ----
            ClientChannel? channel;
            try
            {
                channel = await WaitHealthyAsync(_config.StartupBudget, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (channel is null)
            {
                // Never became healthy: treat as a crash of this incarnation and
                // let the taxonomy/backoff decide below.
                (detection, exitCode) = ObserveDeath();
                if (!await HandleCrashAsync(detection, exitCode, v => backoffAppliedMs = v, ct)
                        .ConfigureAwait(false))
                    return;
                firstStart = false;
                continue;
            }

            _lastRestoredSeq = restoredSeq;
            if (!firstStart)
            {
                // Complete the pending CrashRecord: the replacement is serving.
                _crashLog.Append(new CrashRecord(
                    DateTimeOffset.UtcNow, _config.EngineIdentity, exitCode, detection,
                    restoredSeq is ulong seq ? $"restored({seq})" : "restored(fresh)",
                    backoffAppliedMs));
            }
            _backoff = _config.BackoffInitial; // healthy ⇒ backoff resets
            firstStart = false;

            // ---- ping loop until death or shutdown (FR-020) ----
            // Disposal is manual (not `await using`): the reconnect path swaps
            // the live channel mid-loop, and `using` would dispose the ORIGINAL
            // local, leaking the replacement (CS0728).
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    WriteStatus("healthy");
                    try
                    {
                        await Task.Delay(_config.PingInterval, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (await PingOnceAsync(channel, ct).ConfigureAwait(false))
                    {
                        _lastHeartbeat = DateTimeOffset.UtcNow; // heartbeat recorded (FR-020)
                        continue;
                    }

                    // One fresh connection inside the same timeout budget — a
                    // broken socket with a live engine is not death.
                    var fresh = await TryConnectAndPingAsync(_config.PingTimeout, ct).ConfigureAwait(false);
                    if (fresh is not null)
                    {
                        await channel.DisposeAsync().ConfigureAwait(false);
                        channel = fresh;
                        _lastHeartbeat = DateTimeOffset.UtcNow;
                        continue;
                    }

                    break; // death detected
                }
            }
            finally
            {
                await channel.DisposeAsync().ConfigureAwait(false);
            }
            if (ct.IsCancellationRequested)
                return;

            (detection, exitCode) = ObserveDeath();
            if (!await HandleCrashAsync(detection, exitCode, v => backoffAppliedMs = v, ct)
                    .ConfigureAwait(false))
                return;
        }
    }

    // ------------------------------------------------------------- crash path

    /// <summary>Record the crash facts, classify, back off. False ⇒ stop the loop.</summary>
    private async Task<bool> HandleCrashAsync(
        CrashDetection detection, int? exitCode, Action<double> backoffApplied, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        _crashTimes.Add(now);
        Console.Error.WriteLine(
            $"glp_supervisor: engine death detected ({detection}, exit={exitCode?.ToString() ?? "n/a"})");
        // Contract step 1 (supervision.md "Crash handling"): the CrashRecord is
        // DURABLE at detection — before backoff/restart — so a supervisor that
        // dies mid-backoff or cycles on a never-healthy replacement still leaves
        // the crash in crash-log.jsonl (FR-022/FR-024). The replacement's outcome
        // lands as the follow-up "restored(seq)" / "unrecoverable(...)" record
        // (append-only completion, contract step 4).
        _crashLog.Append(new CrashRecord(
            now, _config.EngineIdentity, exitCode, detection, "restarting", 0));

        if (UnrecoverableTaxonomy.IsExplicitPoison(exitCode))
        {
            Stop(UnrecoverableReason.ExplicitPoison, (detection, exitCode, 0));
            return false;
        }
        if (UnrecoverableTaxonomy.IsRepeatedImmediateCrash(
                _crashTimes, now, _config.CrashWindow, _config.CrashThreshold))
        {
            Stop(UnrecoverableReason.RepeatedImmediateCrash, (detection, exitCode, 0));
            return false;
        }

        backoffApplied(_backoff.TotalMilliseconds);
        _backoffHistory.Add(_backoff.TotalMilliseconds);
        WriteStatus("restarting");
        try
        {
            await Task.Delay(_backoff, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        var next = TimeSpan.FromMilliseconds(_backoff.TotalMilliseconds * _config.BackoffMultiplier);
        _backoff = next > _config.BackoffMax ? _config.BackoffMax : next;
        return true;
    }

    private (CrashDetection, int?) ObserveDeath()
    {
        if (_child is { HasExited: true })
            return (CrashDetection.Exit, _child.ExitCode);
        // Ping-timeout zombie: the process lives but stopped answering — kill it
        // so the replacement can bind the endpoint.
        try { _child?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        try { _child?.WaitForExit(5_000); } catch (SystemException) { }
        return (CrashDetection.PingTimeout, null);
    }

    private sealed class UnrecoverableException : Exception
    {
        public UnrecoverableReason Reason { get; }
        public UnrecoverableException(UnrecoverableReason reason) { Reason = reason; }
    }

    private void Stop(
        UnrecoverableReason reason,
        (CrashDetection Detection, int? ExitCode, double BackoffMs)? detectionIfPending)
    {
        StoppedReason = UnrecoverableTaxonomy.Word(reason);
        if (detectionIfPending is { } p)
        {
            // Persist the classification on the crash record (FR-023).
            _crashLog.Append(new CrashRecord(
                DateTimeOffset.UtcNow, _config.EngineIdentity, p.ExitCode, p.Detection,
                $"unrecoverable({StoppedReason})", p.BackoffMs));
        }
        WriteStatus($"stopped({StoppedReason})");
        // Loud operator surface — never a silent stop (FR-023).
        Console.Error.WriteLine(
            $"glp_supervisor: UNRECOVERABLE — {StoppedReason}; restart loop STOPPED. " +
            $"Inspect {_config.StoreRoot}\\supervisor\\crash-log.jsonl and the engine store.");
        try { _child?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
    }

    // ------------------------------------------------------------ engine start

    /// <summary>
    /// Start the engine via the restore path: --from-snapshot latest when a
    /// snapshot exists (previous-seq fallback ONCE on a corrupt latest —
    /// DEF-F2), fresh when the store is empty. Returns the restored seq (null
    /// for a fresh start). Throws UnrecoverableException per the taxonomy.
    /// </summary>
    private async Task<ulong?> StartEngineWithRestoreFallbackAsync(CancellationToken ct)
    {
        // store_unavailable is a TAXONOMY verdict ("both backends down" — DEF-F2),
        // not a first-IOException verdict: a transient manifest-read race (the
        // engine child atomically replacing manifest.json mid-read) must not stop
        // the restart loop permanently. Bounded retries absorb the transient case;
        // only a persistent failure classifies (codexreview 20260730T070051Z
        // transient-read-classified-unrecoverable). The PGLite connection is
        // disposed per attempt — the old path leaked one open connection to the
        // bridge-guarded cluster per restart cycle.
        IReadOnlyList<SnapshotMeta>? snapshots = null;
        Exception? lastStoreError = null;
        for (int attempt = 0; attempt < 3 && snapshots is null; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(250, ct).ConfigureAwait(false);
            NpgsqlSnapshotDb? db = null;
            try
            {
                var fileBackend = new FileSnapshotStore(_config.StoreRoot, _config.EngineIdentity);
                ISnapshotBackend? primary = null;
                var pgConn = Environment.GetEnvironmentVariable("GLP_SNAPSHOT_PG_CONN");
                if (!string.IsNullOrWhiteSpace(pgConn))
                {
                    try
                    {
                        db = new NpgsqlSnapshotDb(pgConn);
                        primary = PgliteSnapshotStore.Open(db, _config.EngineIdentity);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            $"glp_supervisor: primary snapshot store unavailable ({ex.Message}) — file fallback only");
                    }
                }
                var store = new SnapshotStore(primary, fileBackend,
                    msg => Console.Error.WriteLine($"glp_supervisor: SNAPSHOT STORE DEGRADED: {msg}"));
                snapshots = store.List();
            }
            catch (Exception ex)
            {
                lastStoreError = ex;
                Console.Error.WriteLine(
                    $"glp_supervisor: snapshot store read failed (attempt {attempt + 1}/3): {ex.Message}");
            }
            finally
            {
                db?.Dispose();
            }
        }
        if (snapshots is null)
        {
            // Persistently unreadable — store_unavailable (DEF-F2).
            Console.Error.WriteLine(
                $"glp_supervisor: snapshot store unavailable: {lastStoreError?.Message}");
            throw new UnrecoverableException(UnrecoverableReason.StoreUnavailable);
        }

        if (snapshots.Count == 0)
        {
            StartChild(fromSnapshot: null);
            return null;
        }

        // Latest first; on a restore failure fall back to the previous seq ONCE.
        var latest = snapshots[^1].Seq;
        if (await TryStartRestoringAsync(latest, ct).ConfigureAwait(false))
            return latest;

        Console.Error.WriteLine(
            $"glp_supervisor: restore from seq={latest} FAILED — falling back to the previous seq once (DEF-F2)");
        if (snapshots.Count >= 2)
        {
            var previous = snapshots[^2].Seq;
            if (await TryStartRestoringAsync(previous, ct).ConfigureAwait(false))
                return previous;
        }
        throw new UnrecoverableException(UnrecoverableReason.CorruptLatestSnapshot);
    }

    /// <summary>Start `--from-snapshot seq`; false when the child died reporting a restore failure.</summary>
    private async Task<bool> TryStartRestoringAsync(ulong seq, CancellationToken ct)
    {
        StartChild(fromSnapshot: seq);
        // A restore failure is loud and immediate: the engine prints RESTORE
        // FAILED and exits before ever listening. Give it a short observation
        // window; a child that is still running (or exited cleanly later) is
        // handled by the healthy-ping path.
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_child is { HasExited: true })
            {
                // Drain the async stderr pump before consulting it: HasExited can
                // observe the exit BEFORE ErrorDataReceived delivered the final
                // lines — the parameterless WaitForExit waits for the redirected
                // streams to complete (codexreview 20260730T070051Z cycle 3
                // restore-failed-marker-stderr-drain-race).
                try { _child.WaitForExit(); } catch (SystemException) { }
                // ONLY a self-reported restore failure triggers the previous-seq
                // fallback — any other pre-listen death (port in use, bad args)
                // is a generic crash for the taxonomy/backoff path, never a
                // silent snapshot demotion.
                lock (_recentStderr)
                {
                    if (_recentStderr.Any(l => l.Contains("RESTORE FAILED", StringComparison.Ordinal)))
                        return false;
                }
                return true; // WaitHealthyAsync observes the exit → crash path
            }
            // Listening yet? A successful bind means the restore completed (the
            // engine restores BEFORE opening the listener).
            var probe = await TryConnectAndPingAsync(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
            if (probe is not null)
            {
                await probe.DisposeAsync().ConfigureAwait(false);
                return true;
            }
        }
        return _child is { HasExited: false };
    }

    private void StartChild(ulong? fromSnapshot)
    {
        lock (_recentStderr) _recentStderr.Clear();
        var args = $"--listen {_config.Listen} --store \"{_config.StoreRoot}\"";
        if (fromSnapshot is ulong seq)
            args += $" --from-snapshot {seq}";

        var psi = new ProcessStartInfo
        {
            FileName = _config.EngineBinary,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var child = Process.Start(psi)
            ?? throw new InvalidOperationException($"failed to start {_config.EngineBinary}");
        child.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) Console.WriteLine($"[engine] {e.Data}");
        };
        child.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            Console.Error.WriteLine($"[engine] {e.Data}");
            lock (_recentStderr)
            {
                _recentStderr.Add(e.Data);
                if (_recentStderr.Count > 50) _recentStderr.RemoveAt(0);
            }
        };
        child.BeginOutputReadLine();
        child.BeginErrorReadLine();
        _child?.Dispose(); // the superseded (exited/killed) child's process handle
        _child = child;
        WriteStatus("starting");
    }

    // ------------------------------------------------------------------- pings

    private async Task<ClientChannel?> WaitHealthyAsync(TimeSpan budget, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + budget;
        while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (_child is { HasExited: true })
                return null;
            var channel = await TryConnectAndPingAsync(_config.PingTimeout, ct).ConfigureAwait(false);
            if (channel is not null)
            {
                _lastHeartbeat = DateTimeOffset.UtcNow;
                return channel;
            }
            await Task.Delay(200, ct).ConfigureAwait(false);
        }
        return null;
    }

    private async Task<bool> PingOnceAsync(ClientChannel channel, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_config.PingTimeout);
        try
        {
            var response = await channel.RoundTripAsync(
                RequestFrame.Empty(channel.NextRequestId(), RequestKind.Ping), timeout.Token)
                .ConfigureAwait(false);
            return response.Kind == ResponseKind.Ack;
        }
        catch (Exception ex) when (ex is ClientTransportException or OperationCanceledException
                                       or SplitProtocolException or IOException)
        {
            return ct.IsCancellationRequested
                ? throw new OperationCanceledException(ct)
                : false;
        }
    }

    private async Task<ClientChannel?> TryConnectAndPingAsync(TimeSpan budget, CancellationToken ct)
    {
        try
        {
            var channel = await ClientChannel.ConnectAsync(_host, _port, budget).ConfigureAwait(false);
            if (await PingOnceAsync(channel, ct).ConfigureAwait(false))
                return channel;
            await channel.DisposeAsync().ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ex is ClientTransportException or IOException
                                       or System.Net.Sockets.SocketException)
        {
            return null;
        }
    }

    private void WriteStatus(string engineState) =>
        _crashLog.WriteStatus(new SupervisorStatus(
            DateTimeOffset.UtcNow, _config.EngineIdentity, engineState,
            EnginePid, _lastHeartbeat, _lastRestoredSeq, _crashTimes.Count));

    public override void Dispose()
    {
        try { _child?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        _child?.Dispose();
        base.Dispose();
    }
}
