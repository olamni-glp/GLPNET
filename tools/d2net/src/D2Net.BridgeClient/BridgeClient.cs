using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace D2Net.BridgeClient;

/// <summary>
/// Cross-process bridge discovery + auto-spawn helper, per
/// <c>specs/012-codeconv-runner/contracts/bridge_lifecycle.md</c>.
///
/// Lock semantics: a single OS file lock at <c>&lt;repo-root&gt;/.pgdb.bridge.lock</c>
/// (sibling to <c>.pgdb/</c>; PGLite refuses non-PG files inside its data dir
/// at init time). Won the lock → spawn detached bridge, wait for
/// <c>BRIDGE_READY</c>, release the lock so the bridge can take it. Lost the
/// lock → read sidecar JSON; retry once after 250 ms if absent.
/// </summary>
public static class BridgeClient
{
    public static class ExitCodes
    {
        public const int LockHeld = 5;
    }

    public sealed class BridgeStartupTimeoutException : Exception
    {
        public BridgeStartupTimeoutException(string message) : base(message) { }
    }

    public sealed class BridgeRaceLostException : Exception
    {
        public BridgeRaceLostException(string message) : base(message) { }
    }

    /// <summary>
    /// Acquire the bridge: either become the spawn-coordinator and start one,
    /// or discover an already-running bridge from the sidecar JSON. Always
    /// returns a connectable endpoint.
    /// </summary>
    public static async Task<BridgeEndpoint> AcquireOrDiscover(
        string repoRoot,
        TimeSpan readyTimeout,
        CancellationToken cancellationToken = default)
    {
        var dataDir = Path.Combine(repoRoot, ".pgdb");
        var sidecarPath = Path.Combine(dataDir, "bridge.json");
        var lockPath = Path.Combine(repoRoot, ".pgdb.bridge.lock");

        Directory.CreateDirectory(dataDir);

        FileStream? lockFile = TryAcquireLock(lockPath);
        if (lockFile is not null)
        {
            // Path A: bridge owner. Spawn detached bridge, wait READY, release
            // the lock so the bridge process can acquire its own proper-lockfile
            // lock. Brief race-window between release and bridge acquire is
            // benign — losers exit 5 and the survivor's sidecar is canonical.
            try
            {
                var endpoint = await SpawnAndWaitReady(repoRoot, dataDir, readyTimeout, cancellationToken).ConfigureAwait(false);
                return new BridgeEndpoint(endpoint.host, endpoint.port, endpoint.pid, ownedLock: null);
            }
            finally
            {
                try { lockFile.Dispose(); } catch { /* best effort */ }
            }
        }

        // Path B: bridge consumer. Read sidecar (with one 250 ms retry per
        // contract "Sidecar trust rule").
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var sidecar = SidecarFile.TryRead(sidecarPath);
            if (sidecar is not null)
            {
                return new BridgeEndpoint(sidecar.Host, sidecar.Port, sidecar.Pid, ownedLock: null);
            }
            if (attempt == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new BridgeRaceLostException(
            $"could not acquire bridge lock and no sidecar found at {sidecarPath} after retry");
    }

    private static FileStream? TryAcquireLock(string lockPath)
    {
        try
        {
            return new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Path is a directory (proper-lockfile holds it) → treat as held.
            return null;
        }
    }

    private static async Task<(string host, int port, int pid)> SpawnAndWaitReady(
        string repoRoot,
        string dataDir,
        TimeSpan readyTimeout,
        CancellationToken cancellationToken)
    {
        var bridgeScript = ResolveBridgeScript(repoRoot);
        var node = ResolveNodeExecutable()
            ?? throw new InvalidOperationException("node not found on PATH; install Node.js >= 20");

        var psi = new ProcessStartInfo
        {
            FileName = node,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repoRoot,
        };
        psi.ArgumentList.Add(bridgeScript);
        psi.ArgumentList.Add("--data-dir");
        psi.ArgumentList.Add(dataDir);
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add("0");
        psi.ArgumentList.Add("--daemon");

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Process.Start returned null spawning bridge");

        var deadline = DateTime.UtcNow + readyTimeout;
        try
        {
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (proc.HasExited)
                {
                    var stderr = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"bridge exited with code {proc.ExitCode} before READY. stderr: {stderr}");
                }

                var line = await ReadLineWithTimeoutAsync(
                    proc.StandardOutput,
                    deadline - DateTime.UtcNow,
                    cancellationToken).ConfigureAwait(false);

                if (line is null)
                {
                    continue;
                }

                if (line.StartsWith("BRIDGE_READY ", StringComparison.Ordinal))
                {
                    var parsed = ParseReady(line);
                    return (host: "127.0.0.1", port: parsed.port, pid: parsed.pid);
                }
            }

            throw new BridgeStartupTimeoutException(
                $"timed out waiting for BRIDGE_READY after {readyTimeout.TotalSeconds:F1}s");
        }
        finally
        {
            // Detach from stdio pipes so the bridge can write to bridge.log
            // unimpeded. We do NOT kill the process here.
            try { proc.StandardInput.Close(); } catch { /* ignore */ }
            // Note: closing our read end of stdout/stderr happens via Dispose
            // on the Process, which we own here; once we return the endpoint,
            // 'using' disposes Process — but the spawned child continues
            // running because OS reference counts on pipes are independent.
        }
    }

    private static async Task<string?> ReadLineWithTimeoutAsync(
        StreamReader reader,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (timeout <= TimeSpan.Zero) return null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            return await reader.ReadLineAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static (int port, int pid) ParseReady(string line)
    {
        // BRIDGE_READY port=<int> pid=<int>
        int port = 0, pid = 0;
        foreach (var token in line.Split(' '))
        {
            if (token.StartsWith("port=", StringComparison.Ordinal))
                int.TryParse(token.AsSpan("port=".Length), out port);
            else if (token.StartsWith("pid=", StringComparison.Ordinal))
                int.TryParse(token.AsSpan("pid=".Length), out pid);
        }
        if (port <= 0 || pid <= 0)
            throw new InvalidOperationException($"could not parse BRIDGE_READY line: '{line}'");
        return (port, pid);
    }

    private static string ResolveBridgeScript(string repoRoot)
    {
        var candidate = Path.Combine(repoRoot, "prereq-patterns", "pglite", "pglite_bridge.mjs");
        if (File.Exists(candidate)) return candidate;
        throw new FileNotFoundException(
            $"unified bridge script not found at {candidate}. Repo root mis-detected, or feature 012 not landed.",
            candidate);
    }

    public static string? ResolveNodeExecutable()
    {
        var path = System.Environment.GetEnvironmentVariable("PATH") ?? "";
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
        return null;
    }
}
