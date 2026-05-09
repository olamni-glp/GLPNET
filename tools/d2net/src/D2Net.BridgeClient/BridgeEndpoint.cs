namespace D2Net.BridgeClient;

/// <summary>
/// A connection point to the unified PGLite bridge for the repo. Returned by
/// <see cref="BridgeClient.AcquireOrDiscover"/>. <see cref="OwnedLock"/> is
/// non-null when this caller spawned (or attempted to spawn) the bridge; the
/// lock is released when the endpoint is disposed.
/// </summary>
public sealed class BridgeEndpoint : System.IDisposable
{
    public string Host { get; }
    public int Port { get; }
    public int Pid { get; }
    public bool Owned { get; }

    /// <summary>Underlying file lock; released on Dispose. Null for sidecar consumers.</summary>
    private System.IO.FileStream? _ownedLock;

    public BridgeEndpoint(string host, int port, int pid, System.IO.FileStream? ownedLock)
    {
        Host = host;
        Port = port;
        Pid = pid;
        Owned = ownedLock is not null;
        _ownedLock = ownedLock;
    }

    public void Dispose()
    {
        // Per contract: release the lock if owned, do NOT terminate the bridge.
        try { _ownedLock?.Dispose(); } catch { /* best effort */ }
        _ownedLock = null;
    }
}
