// LinkRewirer — restore-order gating for link re-establishment (US4/T032).
//
// Order (T032, spec US4/AS-1..2): persistent constructs are already restored
// (SnapshotRestore) → each durable link definition re-establishes its transport
// endpoint per the RECORDED role → cursors re-wire at their restored positions
// (RewireHandle.Adopt, DEF-E1) → the drain resumes from the first unshipped
// position.
//
// Peer-unreachable edge case (spec): local work proceeds — establishment runs on
// background tasks with the link layer's existing rendezvous policy (the
// '_link_setup' kernel budgets: listener 180 s, connector 120 s; TcpTransport
// itself retries connection-refused inside that window) while the engine serves
// requests; only that link's drain waits. Adoption (heap-touching wiring) is
// applied ON THE REQUEST THREAD via ApplyReady() at every dispatcher entry — the
// engine host's single-threaded heap discipline is preserved (the supervisor's
// PING traffic makes application prompt even with an idle client).
//
// A definition whose establishment ultimately fails is surfaced LOUDLY (console +
// the Failed list on STATUS) and stays down — the whole restore is never failed
// for one unreachable peer. While any rewire is pending, snapshots are deferred
// (RequestDispatcher) so a not-yet-re-established link can never silently vanish
// from the next snapshot's section 0x09.

using System.Collections.Concurrent;

using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.EngineHost.Snapshot;

public sealed class LinkRewirer : IDisposable
{
    // Same rendezvous budgets as the '_link_setup' kernel (LinkSetupKernel.Establish).
    private static readonly TimeSpan ListenerBudget = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan ConnectorBudget = TimeSpan.FromSeconds(120);

    private readonly GlpRuntimeEngine _rt;
    private readonly LinkRuntime _link;
    private readonly ConcurrentQueue<(RestoredLinkDefinition Def, ILinkEndpoint Endpoint)> _ready = new();
    private readonly List<string> _failed = new();
    // Definitions not yet ADOPTED (establishing, ready, or failed). A failed
    // re-establishment must never make the durable definition vanish from the
    // next snapshot's section 0x09 (codexreview 20260730T070051Z
    // failed-link-definition-dropped-from-future-snapshots): the capture path
    // re-emits these, so the link is retried on the NEXT restore even though
    // this incarnation could not reach the peer.
    private readonly Dictionary<LinkId, RestoredLinkDefinition> _outstanding = new();
    private int _pending;
    private volatile bool _disposed;

    public LinkRewirer(GlpRuntimeEngine rt, LinkRuntime link)
    {
        _rt = rt;
        _link = link;
    }

    /// <summary>
    /// Definitions still awaiting adoption (establishing / ready / failed) —
    /// merged into snapshot section 0x09 by the capture path so an
    /// un-re-established link stays durable. Request-thread only.
    /// </summary>
    public IReadOnlyList<RestoredLinkDefinition> OutstandingDefinitions
    {
        get { lock (_outstanding) return _outstanding.Values.ToArray(); }
    }

    /// <summary>Rewires establishing or established-but-not-yet-adopted. Snapshots defer while &gt; 0.</summary>
    public int Pending => Volatile.Read(ref _pending);

    /// <summary>Loudly-failed re-establishments (link stays down; surfaced via STATUS).</summary>
    public IReadOnlyList<string> Failed
    {
        get { lock (_failed) return _failed.ToArray(); }
    }

    /// <summary>Start background re-establishment for every restored definition.</summary>
    public void Begin(IReadOnlyList<RestoredLinkDefinition> definitions)
    {
        foreach (var def in definitions)
        {
            lock (_outstanding) _outstanding[def.Id] = def;
            Interlocked.Increment(ref _pending);
            _ = Task.Run(() => EstablishAsync(def));
        }
    }

    private async Task EstablishAsync(RestoredLinkDefinition def)
    {
        try
        {
            var transport = _link.Transports.Select(def.Id.Scheme);
            var opts = LinkOptions.Default;
            using var cts = new CancellationTokenSource(
                def.Role == LinkRole.Listener ? ListenerBudget : ConnectorBudget);
            var endpoint = def.Role == LinkRole.Listener
                ? await transport.ListenAsync(def.Id.Scheme, def.Id.Endpoint, opts, cts.Token).ConfigureAwait(false)
                : await transport.ConnectAsync(def.Id.Scheme, def.Id.Endpoint, opts, cts.Token).ConfigureAwait(false);
            if (_disposed)
            {
                // Host shut down while this rendezvous was in flight — nothing will
                // ever ApplyReady; dispose rather than park a live socket forever
                // (cycle-3 dispose/in-flight residual).
                await endpoint.DisposeAsync().ConfigureAwait(false);
                Interlocked.Decrement(ref _pending);
                return;
            }
            _ready.Enqueue((def, endpoint));
            // _pending stays up until ApplyReady adopts it on the request thread.
        }
        catch (Exception ex)
        {
            var why = ex is OperationCanceledException
                ? "rendezvous budget exhausted (peer unreachable?)"
                : ex.Message;
            var line = $"{def.Id}: {why}";
            lock (_failed) _failed.Add(line);
            Interlocked.Decrement(ref _pending);
            Console.Error.WriteLine(
                $"glp_engine_host: LINK RE-ESTABLISH FAILED {line} — local work continues; this link stays down");
        }
    }

    /// <summary>
    /// Adopt every endpoint whose rendezvous completed. Request-thread only — this
    /// is the one place restored links touch the heap (cursor wiring, egress
    /// OnBind, pump registration).
    /// </summary>
    public void ApplyReady()
    {
        while (_ready.TryDequeue(out var item))
        {
            try
            {
                bool preRegistered = _link.Links.Contains(item.Def.Id);
                RewireHandle.Adopt(
                    _rt, _link, item.Def.Id, item.Def.Role, () => item.Endpoint,
                    item.Def.InWriterAddr, item.Def.OutReaderAddr, item.Def.FaultsWriterAddr,
                    item.Def.MonitorCursors, item.Def.EgressShippedCount);
                if (preRegistered)
                {
                    // Idempotent re-adoption: the registry kept the existing handle,
                    // so THIS pre-established endpoint was never wired — dispose it
                    // rather than leak a live socket (codexreview 20260730T070051Z
                    // duplicate-rewire-endpoint-leak).
                    item.Endpoint.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                lock (_outstanding) _outstanding.Remove(item.Def.Id);
                Console.WriteLine($"glp_engine_host: link re-established {item.Def.Id} (drain resumed)");
            }
            catch (Exception ex)
            {
                // Definition stays OUTSTANDING (capturable) — loud, never silently
                // gone. A mid-re-ship fault reports the ACHIEVED shipped count;
                // fold it into the durable definition so a later retry (or the
                // next snapshot) never re-ships the already-delivered elements.
                if (ex is RewireEgressException ree)
                    lock (_outstanding)
                        _outstanding[item.Def.Id] = item.Def with
                        { EgressShippedCount = ree.AchievedShippedCount };
                var line = $"{item.Def.Id}: adoption failed: {ex.Message}";
                lock (_failed) _failed.Add(line);
                Console.Error.WriteLine($"glp_engine_host: LINK RE-WIRE FAILED {line}");
            }
            finally
            {
                Interlocked.Decrement(ref _pending);
            }
        }
    }

    /// <summary>
    /// Host shutdown: dispose every established-but-never-adopted endpoint so no
    /// live socket/listener outlives the host (codexreview 20260730T070051Z
    /// established-endpoint-never-disposed).
    /// </summary>
    public void Dispose()
    {
        _disposed = true; // in-flight rendezvous self-dispose on completion
        while (_ready.TryDequeue(out var item))
        {
            try { item.Endpoint.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch (Exception) { /* best-effort teardown */ }
        }
    }
}
