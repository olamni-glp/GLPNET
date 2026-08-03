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
    /// <param name="Replayed">
    /// 064: this item came from the host's durable log (boot replay), not the wire. It is
    /// delivered to the program through the ORDINARY path — indistinguishable from live traffic —
    /// but the delivery observer is NOT invoked for it, so replay never re-appends what it
    /// replays (FR-005 idempotence).
    /// </param>
    private readonly record struct InboundItem(
        LinkHandle Handle, Term? Value, bool Close, bool Fault, bool Replayed = false);

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
    /// Feature 064 (FR-004, contracts/message-log-and-replay.md) — optional host-owned
    /// delivery observer. Invoked on the RUNNER thread, once per DATA term actually
    /// delivered to the program (post-reassembly, post-ordering, post-dedup), immediately
    /// BEFORE the heap bind that lets the program observe it — so a host that persists
    /// here is durable before the program can act. Never invoked for graceful-close or
    /// fault items. <c>null</c> (the default) ⇒ behavior byte-identical to pre-064.
    /// An observer that throws must not corrupt the ingress: the throw is caught and
    /// surfaced, and delivery proceeds (availability over durability — the both-backends
    /// -fail policy in the contract).
    /// </summary>
    public Action<LinkId, Term>? OnDelivered { get; set; }

    /// <summary>
    /// Feature 064 (FR-005, contracts/message-log-and-replay.md) — optional boot-replay source.
    /// When set, <see cref="AddLink"/> drains it into the inbox for the new link BEFORE starting
    /// that link's receive loop, so the program observes its durable history first, through the
    /// ordinary delivery path, and only then any live traffic (history-precedes-live, with no
    /// window in which a fast peer could interleave). Replayed items never reach
    /// <see cref="OnDelivered"/>. <c>null</c> (the default) ⇒ no replay.
    /// </summary>
    public Func<LinkId, IEnumerable<Term>>? ReplaySource { get; set; }

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
        // 064: drain the durable history into the inbox BEFORE the receive loop starts, so the
        // program sees history first and a fast peer cannot interleave live traffic into it.
        if (ReplaySource is { } replay)
        {
            foreach (var term in replay(handle.Id))
                _inbox.Add(new InboundItem(handle, term, Close: false, Fault: false, Replayed: true), _cts.Token);
        }
        // _faultSubs + _recvLoops share one lock: Dispose reads both to detach handlers and join loops.
        lock (_faultSubs)
        {
            _faultSubs.Add((handle.Endpoint, onFault));
            _recvLoops.Add(Task.Run(() => RecvLoopAsync(handle, _cts.Token)));
        }
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

        // 064: durability precedes observation — the host-owned observer (if any) sees this
        // delivered term BEFORE the bind below makes it visible to the program (FR-004).
        // A REPLAYED item is skipped: it came from the log, so re-appending it would break
        // replay idempotence (FR-005 / SC-002's byte-identical second restart).
        if (!item.Replayed && OnDelivered is { } observer)
        {
            try
            {
                observer(item.Handle.Id, item.Value!);
            }
            catch (Exception e)
            {
                // Loud, never silent; delivery still proceeds (contract's both-backends-fail policy).
                Console.WriteLine($"[link ingress] delivery observer failed: {e.Message}");
            }
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

                try
                {
                    // The FRAME-processing layers below all consume UNTRUSTED remote bytes and loud-fail
                    // on a malformed/adversarial frame (CRC/length mismatch, reorder-buffer overflow,
                    // too-many-in-flight) — all remote-triggerable through the QUIC AEAD. Guard the WHOLE
                    // parse→reassemble→reorder→decode pipeline so any such fault becomes an OBSERVABLE
                    // recorded fault (FR-007/FR-016), never a silent death of the fire-and-forget task.
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
                            // A malformed / undecodable inbound payload (truncated/tampered envelope).
                            // Surface a recorded, OBSERVABLE fault for just this item (FR-016) and keep
                            // decoding the rest of the ordered batch.
                            EnqueueFault(handle, new LinkFaultSignal(handle.Id, LinkFaultKind.Permanent,
                                $"malformed inbound payload: {ex.Message}"));
                            continue;
                        }
                        _inbox.Add(new InboundItem(handle, term, Close: false, Fault: false), ct);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A malformed FRAME at the parse / reassembly / ordering layer — surface it as an
                    // observable permanent fault instead of letting it escape and silently kill the
                    // receive task, then keep the link alive for subsequent frames.
                    EnqueueFault(handle, new LinkFaultSignal(handle.Id, LinkFaultKind.Permanent,
                        $"malformed inbound frame: {ex.Message}"));
                    continue;
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

        // Detach every OnFault subscription so no endpoint keeps this pump alive (review-fix), and
        // snapshot the receive loops — both under the shared lock.
        Task[] loops;
        lock (_faultSubs)
        {
            foreach (var (endpoint, handler) in _faultSubs)
                endpoint.OnFault -= handler;
            _faultSubs.Clear();
            loops = _recvLoops.ToArray();
        }

        // Join the background receive loops (bounded) so none outlives the pump — they observe the
        // cancelled token / completed inbox and exit. Their cancellation faults are expected.
        try { Task.WaitAll(loops, TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { /* loops ending on cancellation/teardown — expected */ }

        _inbox.Dispose();
        _cts.Dispose();
    }
}
