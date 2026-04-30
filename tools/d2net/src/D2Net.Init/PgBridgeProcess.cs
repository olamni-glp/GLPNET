using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace D2Net.Init;

/// <summary>
/// Per-invocation lifecycle wrapper for the vendored <c>bridge-direct.mjs</c>
/// Node.js subprocess that exposes PGLite via the Postgres wire protocol.
///
/// Spec: specs/005-d2net-pglite-bridge/contracts/pgbridge-contract.md
/// Behaviour:
///   - <see cref="StartAsync"/> spawns node, waits up to 15 s for the
///     "BRIDGE_READY port=&lt;port&gt; pid=&lt;pid&gt;" handshake (FR-005).
///   - On <c>BRIDGE_ERROR</c>, the verbatim message is exposed via
///     <see cref="LastBridgeError"/>; the caller decides which exit code
///     to use (the <c>pglite_init_failed</c> case maps to
///     <see cref="ExitCodes.DbOpenFailed"/> with the FR-005 recovery hint).
///   - <see cref="Dispose"/> runs the FR-006 staged shutdown:
///     close stdin -&gt; wait 5 s -&gt; SIGTERM-equivalent -&gt; wait 2 s -&gt; hard kill.
///     A non-fatal warning is written to <see cref="WarningsWriter"/> if the
///     process required forced termination but the workspace mutation
///     completed (the caller's exit code is unchanged).
/// </summary>
public sealed class PgBridgeProcess : IDisposable
{
    /// <summary>
    /// FR-005 fixes the production budget at 15 seconds. Tests that spawn
    /// the bridge in rapid succession can run into PGLite WASM cold-init
    /// times near or beyond that budget; setting the env var
    /// <c>D2NET_BRIDGE_READY_TIMEOUT_SECONDS</c> overrides the budget for
    /// the current process only. This MUST NOT be set in production.
    /// </summary>
    private static readonly TimeSpan ReadyTimeout = ResolveReadyTimeout();
    private static readonly TimeSpan StdinCloseGrace = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SigtermGrace = TimeSpan.FromSeconds(2);

    private static TimeSpan ResolveReadyTimeout()
    {
        var v = Environment.GetEnvironmentVariable("D2NET_BRIDGE_READY_TIMEOUT_SECONDS");
        if (!string.IsNullOrEmpty(v) && int.TryParse(v, out var s) && s > 0 && s <= 600)
            return TimeSpan.FromSeconds(s);
        return TimeSpan.FromSeconds(15);
    }

    private Process? _process;
    private Task? _stdoutTask;
    private Task? _stderrTask;
    private readonly StringBuilder _stderrBuf = new();
    private readonly TaskCompletionSource<BridgeReadyOutcome> _ready = new();

    public int Port { get; }
    public string DataDir { get; }
    public TextWriter WarningsWriter { get; }
    public string? LastBridgeError { get; private set; }

    private PgBridgeProcess(int port, string dataDir, TextWriter warnings)
    {
        Port = port;
        DataDir = dataDir;
        WarningsWriter = warnings;
    }

    /// <summary>
    /// Locate the vendored bridge bundle. Searches alongside the running
    /// assembly first (the production case where MSBuild copied
    /// <c>pgbridge/**</c> into the build output), then falls back to the
    /// project source tree (development inner-loop).
    /// </summary>
    public static string ResolveBridgeBundleDir()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "pgbridge"),
            Path.Combine(Path.GetDirectoryName(typeof(PgBridgeProcess).Assembly.Location) ?? "", "pgbridge"),
        };
        foreach (var c in candidates)
        {
            var script = Path.Combine(c, "bridge-direct.mjs");
            if (File.Exists(script)) return c;
        }
        // Last resort: source tree relative to the assembly (dev inner-loop / unit tests run-without-deploy).
        var asmDir = Path.GetDirectoryName(typeof(PgBridgeProcess).Assembly.Location);
        if (asmDir is not null)
        {
            // tests/<TestProj>/bin/Debug/net8.0 -> ../../../../src/D2Net.Init/pgbridge
            for (int up = 1; up <= 6; up++)
            {
                var probe = asmDir;
                for (int i = 0; i < up; i++) probe = Path.GetDirectoryName(probe) ?? probe;
                var src = Path.Combine(probe, "src", "D2Net.Init", "pgbridge");
                if (File.Exists(Path.Combine(src, "bridge-direct.mjs"))) return src;
            }
        }
        return candidates[0]; // return the canonical path even if missing; caller fails with BridgeBundleMissing.
    }

    public static string ResolveNodeExecutable()
    {
        // PATH lookup honoring PATHEXT on Windows.
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var dirs = path.Split(Path.PathSeparator);
        var exts = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { ".exe", ".cmd", ".bat", "" }
            : new[] { "" };
        foreach (var d in dirs)
        {
            if (string.IsNullOrEmpty(d)) continue;
            foreach (var ext in exts)
            {
                var p = Path.Combine(d, "node" + ext);
                if (File.Exists(p)) return p;
            }
        }
        return ""; // empty -> not found
    }

    public static async Task<PgBridgeProcess> StartAsync(
        int port,
        string dataDir,
        TextWriter warnings,
        CancellationToken cancellationToken = default)
    {
        var bridge = new PgBridgeProcess(port, dataDir, warnings);
        bridge.SpawnAndWait(cancellationToken);
        var outcome = await Task.WhenAny(bridge._ready.Task, Task.Delay(ReadyTimeout, cancellationToken)).ConfigureAwait(false);
        if (outcome != bridge._ready.Task)
        {
            // Timeout. Kill and raise.
            bridge.LastBridgeError = bridge.LastBridgeError ?? $"timed out waiting for BRIDGE_READY (>{ReadyTimeout.TotalSeconds:F0}s)";
            bridge.HardKillIfAlive();
            throw new BridgeStartException(BridgeStartFailureKind.ReadyTimeout, bridge.LastBridgeError, bridge);
        }
        var ready = await bridge._ready.Task.ConfigureAwait(false);
        if (ready.Kind != BridgeReadyOutcomeKind.Ready)
        {
            bridge.HardKillIfAlive();
            throw new BridgeStartException(MapReadyKind(ready.Kind), ready.Message ?? "(no message)", bridge);
        }
        return bridge;
    }

    private void SpawnAndWait(CancellationToken cancellationToken)
    {
        var bundleDir = ResolveBridgeBundleDir();
        var script = Path.Combine(bundleDir, "bridge-direct.mjs");
        var nodeModules = Path.Combine(bundleDir, "node_modules");
        if (!File.Exists(script) || !Directory.Exists(nodeModules))
        {
            LastBridgeError = $"bundle missing: expected {script} and {nodeModules}";
            throw new BridgeStartException(BridgeStartFailureKind.BundleMissing, LastBridgeError, this);
        }

        var node = ResolveNodeExecutable();
        if (string.IsNullOrEmpty(node))
        {
            LastBridgeError = "node not found on PATH";
            throw new BridgeStartException(BridgeStartFailureKind.NodeMissing, LastBridgeError, this);
        }

        var psi = new ProcessStartInfo
        {
            FileName = node,
            WorkingDirectory = bundleDir,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add("--pgdir");
        psi.ArgumentList.Add(DataDir);
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(Port.ToString(System.Globalization.CultureInfo.InvariantCulture));

        _process = Process.Start(psi)
            ?? throw new BridgeStartException(BridgeStartFailureKind.SpawnFailed, "Process.Start returned null", this);

        _stdoutTask = Task.Run(() => ReadStdoutAsync(_process, cancellationToken));
        _stderrTask = Task.Run(() => ReadStderrAsync(_process, cancellationToken));
    }

    private async Task ReadStdoutAsync(Process p, CancellationToken ct)
    {
        try
        {
            string? line;
            while ((line = await p.StandardOutput.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                if (line.StartsWith("BRIDGE_READY ", StringComparison.Ordinal))
                {
                    _ready.TrySetResult(new BridgeReadyOutcome(BridgeReadyOutcomeKind.Ready, line));
                }
                else if (line.StartsWith("BRIDGE_ERROR ", StringComparison.Ordinal))
                {
                    var msg = line.Substring("BRIDGE_ERROR ".Length);
                    LastBridgeError = msg;
                    _ready.TrySetResult(new BridgeReadyOutcome(ClassifyBridgeError(msg), msg));
                }
            }
        }
        catch
        {
            // Silent: the dispose path will report.
        }
        finally
        {
            // If the bridge ended without ever printing a recognizable line, mark it as unexpected exit.
            _ready.TrySetResult(new BridgeReadyOutcome(BridgeReadyOutcomeKind.UnexpectedExit, LastBridgeError ?? "bridge exited without BRIDGE_READY"));
        }
    }

    private async Task ReadStderrAsync(Process p, CancellationToken ct)
    {
        try
        {
            string? line;
            while ((line = await p.StandardError.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                lock (_stderrBuf) { _stderrBuf.AppendLine(line); }
            }
        }
        catch { /* silent */ }
    }

    private static BridgeReadyOutcomeKind ClassifyBridgeError(string message)
    {
        if (message.StartsWith("pglite_init_failed", StringComparison.Ordinal))
            return BridgeReadyOutcomeKind.PgliteInitFailed;
        if (message.StartsWith("listen ", StringComparison.Ordinal)
            && (message.Contains("EADDRINUSE", StringComparison.Ordinal) || message.Contains("address already in use", StringComparison.OrdinalIgnoreCase)))
            return BridgeReadyOutcomeKind.PortInUse;
        return BridgeReadyOutcomeKind.OtherBridgeError;
    }

    private static BridgeStartFailureKind MapReadyKind(BridgeReadyOutcomeKind k) => k switch
    {
        BridgeReadyOutcomeKind.PgliteInitFailed => BridgeStartFailureKind.PgliteInitFailed,
        BridgeReadyOutcomeKind.PortInUse        => BridgeStartFailureKind.PortInUse,
        BridgeReadyOutcomeKind.UnexpectedExit   => BridgeStartFailureKind.UnexpectedExit,
        _                                       => BridgeStartFailureKind.OtherBridgeError,
    };

    public string CapturedStderr() { lock (_stderrBuf) return _stderrBuf.ToString(); }

    public void Dispose()
    {
        if (_process is null) return;
        var p = _process;
        _process = null;

        if (p.HasExited) return;

        // Stage 1: close stdin (graceful shutdown signal per pgbridge-contract).
        try { p.StandardInput.Close(); } catch { /* ignore */ }

        if (p.WaitForExit((int)StdinCloseGrace.TotalMilliseconds)) return;

        // Stage 2: SIGTERM-equivalent.
        try { p.Kill(entireProcessTree: false); } catch { /* ignore */ }
        WarningsWriter.WriteLine($"[pgbridge] warning: bridge pid {SafePid(p)} did not exit on stdin close; sent terminate signal.");

        if (p.WaitForExit((int)SigtermGrace.TotalMilliseconds)) return;

        // Stage 3: hard kill.
        try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
        WarningsWriter.WriteLine($"[pgbridge] warning: bridge pid {SafePid(p)} required hard kill.");
        try { p.WaitForExit(2000); } catch { /* ignore */ }
    }

    private void HardKillIfAlive()
    {
        if (_process is null) return;
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch { }
    }

    private static int SafePid(Process p) { try { return p.Id; } catch { return -1; } }
}

public enum BridgeReadyOutcomeKind
{
    Ready,
    PgliteInitFailed,
    PortInUse,
    OtherBridgeError,
    UnexpectedExit,
}

public sealed record BridgeReadyOutcome(BridgeReadyOutcomeKind Kind, string? Message);

public enum BridgeStartFailureKind
{
    NodeMissing,
    BundleMissing,
    SpawnFailed,
    ReadyTimeout,
    PgliteInitFailed,
    PortInUse,
    OtherBridgeError,
    UnexpectedExit,
}

public sealed class BridgeStartException : Exception
{
    public BridgeStartFailureKind Kind { get; }
    public PgBridgeProcess? Bridge { get; }

    public BridgeStartException(BridgeStartFailureKind kind, string message, PgBridgeProcess? bridge)
        : base(message)
    {
        Kind = kind;
        Bridge = bridge;
    }
}
