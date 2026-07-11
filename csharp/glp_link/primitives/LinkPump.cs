using System.Collections.Concurrent;

using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The inbound pump (feature 025, Option B — design ref
/// <c>research/inbound-pump-and-isolate-manager.md</c> §1.3): the bridge that lets
/// asynchronous transports feed the single-threaded GLP runner without breaking the
/// Option-C single-owner-heap invariant.
/// </summary>
/// <remarks>
/// Discipline (design ref §1.3): a per-link <b>background</b> receive loop decodes
/// arriving frames and does a thread-safe <see cref="ConcurrentQueue{T}"/>-backed
/// <b>enqueue</b> into the one shared <c>inbox</c> — it never touches the heap. The
/// engine's run-to-quiescence driver calls <see cref="TryApplyNext"/> <b>on the
/// runner thread</b>, which dequeues one item and extends the target link's <c>In</c>
/// stream (allocate a fresh pair, cons the value, bind the writer, enqueue the
/// reactivated goals) — exactly the §1.6 B5/B6 worked example. The <c>inbox</c> is
/// the sole cross-thread structure, mirroring Option-C's per-agent channel.
/// </remarks>
public sealed class LinkPump : IInboundPump, IDisposable
{
    /// <summary>
    /// One decoded inbound event awaiting application on the runner thread: an inbound
    /// data term (<c>In</c>-stream extension), a graceful close, or a fault term to fan
    /// out to the link's monitor cursors (<see cref="Fault"/>, T034).
    /// </summary>
    private readonly record struct InboundItem(LinkHandle Handle, Term? Value, bool Close, bool Fault);

    private readonly GlpRuntimeEngine _engine;
    private readonly BlockingCollection<InboundItem> _inbox = new(new ConcurrentQueue<InboundItem>());
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _recvLoops = new();
    // Every OnFault subscription, so Dispose can detach it (review-fix): otherwise the closure keeps
    // this pump — and the engine — reachable via a long-lived endpoint's event.
    private readonly List<(ILinkEndpoint Endpoint, Action<LinkFaultSignal> Handler)> _faultSubs = new();
    private int _liveLinks;

    public LinkPump(GlpRuntimeEngine engine) =>
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

    /// <summary>
    /// Register an established link with the pump: bump the live-link count and start
    /// its background receive loop. Called by <c>'_link_setup'</c> after the handle's
    /// <see cref="LinkHandle.InWriterAddr"/> ingress cursor is wired.
    /// </summary>
    public void AddLink(LinkHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.InWriterAddr is null)
            throw new InvalidOperationException("AddLink before the In-stream ingress cursor was wired");
        Interlocked.Increment(ref _liveLinks);
        // Out-of-band transport faults (FR-008): refine the seam signal to its GLP
        // lattice term and enqueue it for runner-thread fan-out onto the monitor
        // cursors. The handler runs on the transport's thread but only touches the
        // thread-safe inbox — never the heap (T034).
        Action<LinkFaultSignal> onFault = signal => EnqueueFault(handle, signal);
        handle.Endpoint.OnFault += onFault;
        lock (_faultSubs) _faultSubs.Add((handle.Endpoint, onFault));
        _recvLoops.Add(Task.Run(() => RecvLoopAsync(handle, _cts.Token)));
    }

    /// <summary>
    /// Refine an out-of-band seam fault to its GLP lattice term and enqueue it for
    /// runner-thread delivery (T034). Builds the term off the heap (a <see cref="Term"/>
    /// is pure data) and only touches the thread-safe inbox; a fault arriving after the
    /// pump is disposed is dropped (the link is being torn down anyway).
    /// </summary>
    private void EnqueueFault(LinkHandle handle, LinkFaultSignal signal)
    {
        var term = LinkTerms.FromSignal(signal);
        try
        {
            _inbox.Add(new InboundItem(handle, term, Close: false, Fault: true), _cts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException or ObjectDisposedException)
        {
            // Pump disposed / inbox completed mid-teardown — nothing left to deliver to.
        }
    }

    /// <inheritdoc/>
    public bool HasPendingOrLive => _inbox.Count > 0 || Volatile.Read(ref _liveLinks) > 0;

    /// <inheritdoc/>
    public bool TryApplyNext(TimeSpan wait)
    {
        int ms = wait <= TimeSpan.Zero ? 0 : (int)Math.Min(wait.TotalMilliseconds, int.MaxValue);
        if (!_inbox.TryTake(out var item, ms))
            return false;

        var heap = _engine.Heap;

        if (item.Fault)
        {
            // Fan the refined fault term out to every monitor cursor of this link — the
            // establishment `Faults` stream and each `link_monitor` stream (FR-008). A
            // fault is delivered as a bound term, never a verdict (FR-043) and never a
            // logical Fail (FR-044).
            LinkFaults.DeliverFault(heap, _engine, item.Handle, item.Value!);
            return true;
        }

        int cursor = item.Handle.InWriterAddr!.Value;

        if (item.Close)
        {
            // Graceful peer close: end the In stream with [] (nil). A consumer reading
            // the stream sees end-of-stream and reduces its `[]` clause (design ref §1.6 Bn).
            foreach (var act in heap.BindVariable(cursor, new ConstTerm("nil")))
                _engine.EnqueueReactivatedGoal(act);
            item.Handle.InWriterAddr = null; // stream terminated; no further extension
            return true;
        }

        // Extend the In stream by one ground term (design ref §1.6 B6): mint a fresh
        // (writer, reader) pair, cons [value | reader], bind the current writer, wake
        // any suspended reader, then advance the cursor to the fresh writer.
        var (freshWriter, freshReader) = heap.AllocateVariable();
        var cons = new StructTerm(".", new Term[] { item.Value!, new VarRef(freshReader) });
        foreach (var act in heap.BindVariable(cursor, cons))
            _engine.EnqueueReactivatedGoal(act);
        item.Handle.InWriterAddr = freshWriter;
        return true;
    }

    /// <summary>
    /// The per-link background receive loop (never touches the heap): pull a frame,
    /// CRC-validate + reassemble + reorder it through the Phase-2 sublayer, decode the
    /// ground payload, and hand it to the inbox. A <c>null</c> frame is the peer's
    /// graceful close. The decoder rejects any embedded variable — the base layer is
    /// pure ground-relay (FR-010), so a non-ground payload is a wire-contract
    /// violation, not something to localize.
    /// </summary>
    private async Task RecvLoopAsync(LinkHandle handle, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                byte[]? frame = await handle.Endpoint.RecvBytesAsync(ct).ConfigureAwait(false);
                if (frame is null)
                {
                    _inbox.Add(new InboundItem(handle, null, Close: true, Fault: false), ct);
                    return;
                }

                var parsed = FrameCodec.ParseFrame(frame);
                byte[]? payload = handle.Reassembler.Accept(parsed);
                if (payload is null)
                    continue; // awaiting more fragments

                foreach (var ordered in handle.Ordering.Accept(parsed.MessageId, payload))
                {
                    // Decode via the link's payload codec (feature 050): the default ground-relay
                    // blob for loopback/tcp; the 041 crdtmsg envelope for a "quic" link. Loud-fail
                    // on a malformed payload is the codec's contract (FR-007).
                    Term term;
                    try
                    {
                        term = handle.Codec.Decode(ordered);
                    }
                    catch (CapabilityRefusedException ex)
                    {
                        // Verify-before-act refused this inbound gated action (feature 050 US3,
                        // FR-009): the gate RECORDED the distinct refusal before throwing. Refuse
                        // just this action — deliver nothing — and keep the link, the pump, and
                        // the run graceful (never a crash, never a torn-down sibling).
                        Console.WriteLine($"[link capability] inbound gated action refused on {handle.Id}: {ex.Message}");
                        continue;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // A malformed / undecodable inbound payload (e.g. a truncated or tampered
                        // crdtmsg envelope). The codec loud-fails by contract (FR-007); glp_link is
                        // codec-agnostic so it cannot name the codec's concrete exception type. Surface
                        // the fault as a recorded, OBSERVABLE permanent fault on the link's monitor
                        // stream (FR-016 — faults reported, never swallowed) instead of letting it
                        // escape the fire-and-forget receive task and silently kill the link. Refuse
                        // just this frame; the link/pump/run stay graceful.
                        EnqueueFault(handle, new LinkFaultSignal(handle.Id, LinkFaultKind.Permanent,
                            $"malformed inbound payload: {ex.Message}"));
                        continue;
                    }
                    _inbox.Add(new InboundItem(handle, term, Close: false, Fault: false), ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // pump disposed / link torn down — normal shutdown.
        }
        finally
        {
            Interlocked.Decrement(ref _liveLinks);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _inbox.CompleteAdding();

        // Detach every OnFault subscription so no endpoint keeps this pump alive (review-fix).
        lock (_faultSubs)
        {
            foreach (var (endpoint, handler) in _faultSubs)
                endpoint.OnFault -= handler;
            _faultSubs.Clear();
        }

        // Join the background receive loops (bounded) so none outlives the pump — they observe the
        // cancelled token / completed inbox and exit. Their cancellation faults are expected.
        try { Task.WaitAll(_recvLoops.ToArray(), TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { /* loops ending on cancellation/teardown — expected */ }

        _inbox.Dispose();
        _cts.Dispose();
    }
}
