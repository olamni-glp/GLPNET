using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using D2Net.BridgeClient;

namespace D2Net.Init;

/// <summary>
/// Compatibility shim over <see cref="D2Net.BridgeClient.BridgeClient"/>
/// (feature 012 / T038). The bridge is now repo-wide at
/// <c>&lt;repo-root&gt;/.pgdb/</c>, started on demand via cross-process file
/// lock + auto-spawn. <see cref="StartAsync"/> still takes <c>port</c> and
/// <c>dataDir</c> for source-compatibility but their semantics changed:
///
/// <list type="bullet">
///   <item><description>The <c>port</c> argument is ignored. The unified
///   bridge always listens on an OS-allocated ephemeral port. Read the actual
///   port from <see cref="Port"/> after start.</description></item>
///   <item><description>The <c>dataDir</c> argument is interpreted only as a
///   hint to derive the repo root: callers historically pass either
///   <c>&lt;repo&gt;/.D2NET/pgdb</c> (legacy) or
///   <c>&lt;repo&gt;/.pgdb</c> (new). Both resolve to the same repo root, so
///   either works during the migration window.</description></item>
///   <item><description><see cref="Dispose"/> no longer terminates the
///   bridge — it keeps running for other tools. Disposing only releases this
///   client's transient lock (already released after spawn coordination, so
///   typically a no-op).</description></item>
/// </list>
/// </summary>
public sealed class PgBridgeProcess : IDisposable
{
    private static readonly TimeSpan ReadyTimeout = ResolveReadyTimeout();

    private readonly BridgeEndpoint _endpoint;

    public int Port => _endpoint.Port;
    public string DataDir { get; }
    public TextWriter WarningsWriter { get; }
    public string? LastBridgeError { get; private set; }

    private PgBridgeProcess(BridgeEndpoint endpoint, string dataDir, TextWriter warnings)
    {
        _endpoint = endpoint;
        DataDir = dataDir;
        WarningsWriter = warnings;
    }

    private static TimeSpan ResolveReadyTimeout()
    {
        var v = Environment.GetEnvironmentVariable("D2NET_BRIDGE_READY_TIMEOUT_SECONDS");
        if (!string.IsNullOrEmpty(v) && int.TryParse(v, out var s) && s > 0 && s <= 600)
            return TimeSpan.FromSeconds(s);
        return TimeSpan.FromSeconds(30); // default bumped for cold-PGLite-init worst case
    }

    public static async Task<PgBridgeProcess> StartAsync(
        int port,
        string dataDir,
        TextWriter warnings,
        CancellationToken cancellationToken = default)
    {
        _ = port; // shim: ignored, see class doc.
        var repoRoot = ResolveRepoRootFromDataDir(dataDir);
        try
        {
            var endpoint = await BridgeClient.BridgeClient.AcquireOrDiscover(repoRoot, ReadyTimeout, cancellationToken)
                .ConfigureAwait(false);
            return new PgBridgeProcess(endpoint, dataDir, warnings);
        }
        catch (BridgeClient.BridgeClient.BridgeStartupTimeoutException ex)
        {
            throw new BridgeStartException(BridgeStartFailureKind.ReadyTimeout, ex.Message, null);
        }
        catch (BridgeClient.BridgeClient.BridgeRaceLostException ex)
        {
            throw new BridgeStartException(BridgeStartFailureKind.UnexpectedExit, ex.Message, null);
        }
        catch (FileNotFoundException ex)
        {
            throw new BridgeStartException(BridgeStartFailureKind.BundleMissing, ex.Message, null);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("node not found", StringComparison.OrdinalIgnoreCase))
        {
            throw new BridgeStartException(BridgeStartFailureKind.NodeMissing, ex.Message, null);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("pglite_init_failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new BridgeStartException(BridgeStartFailureKind.PgliteInitFailed, ex.Message, null);
        }
        catch (Exception ex)
        {
            throw new BridgeStartException(BridgeStartFailureKind.OtherBridgeError, ex.Message, null);
        }
    }

    private static string ResolveRepoRootFromDataDir(string dataDir)
    {
        // Legacy callers pass <repo>/.D2NET/pgdb; new callers pass <repo>/.pgdb.
        // Both resolve to the same repo root (parent of .D2NET, parent of .pgdb).
        var name = Path.GetFileName(dataDir);
        var parent = Path.GetDirectoryName(dataDir);
        if (parent is null) return Directory.GetCurrentDirectory();
        if (string.Equals(name, "pgdb", StringComparison.Ordinal)
            && string.Equals(Path.GetFileName(parent), ".D2NET", StringComparison.Ordinal))
        {
            return Path.GetDirectoryName(parent) ?? parent;
        }
        if (string.Equals(name, ".pgdb", StringComparison.Ordinal))
        {
            return parent;
        }
        // Unknown layout — assume dataDir's parent is the repo root.
        return parent;
    }

    public string CapturedStderr() => string.Empty;

    public void Dispose()
    {
        try { _endpoint.Dispose(); } catch { /* best effort */ }
    }
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
