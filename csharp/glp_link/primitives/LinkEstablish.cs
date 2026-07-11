using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The ONE canonical establish-and-wire core every establishment path converges on
/// (feature 025, T033): listen/connect (<c>'_link_setup'</c>), and the path-B
/// handshake (<c>'_link_request'</c> connector + <c>'_link_accept'</c> adopting a
/// pending connection). Centralizing it is what makes the two FR-002 paths yield an
/// INDISTINGUISHABLE established link and closes risk R-5 ("two paths, one registry"):
/// given a ground <see cref="LinkId"/>, an endpoint factory, and the three heap stream
/// holes, it registers the handle idempotently (FR-007), wires the In/Out/Faults
/// cursors, arms the Out egress drainer, starts the ingress pump, and installs it.
/// </summary>
public static class LinkEstablish
{
    private const string ListCons = ".";
    private const string Nil = "nil";

    /// <summary>
    /// Establish (or idempotently reuse) the link for <paramref name="id"/> and wire it
    /// to the three heap holes. <paramref name="establish"/> runs only on first
    /// establishment (it opens/adopts the transport endpoint). Returns
    /// <see cref="BodyKernelResult.Success"/> once wired, or
    /// <see cref="BodyKernelResult.Abort"/> on a malformed hole / transport failure /
    /// FR-007 re-establishment (cell-aliasing unspecified — surfaced, not guessed).
    /// </summary>
    public static BodyKernelResult WireEstablishedLink(
        GlpRuntimeEngine rt, LinkRuntime link, LinkId id, Func<ILinkEndpoint> establish,
        object? inArg, object? outArg, object? faultsArg, string who)
    {
        var heap = rt.Heap;

        // Resolve the three output cells to their RAW heap addresses. Use the bare arg
        // VarRef address, NOT heap.Dereference: a local reader dereferences to its paired
        // WRITER (DerefAddr canonicalizes reader→writer), erasing the Out? reader identity.
        if (inArg is not VarRef inVr || !heap.IsWriter(inVr.Addr))
            return Abort(who, "In must be an unbound writer cell");
        if (outArg is not VarRef outVr || !heap.IsReader(outVr.Addr))
            return Abort(who, "Out? must be an unbound reader cell");
        if (faultsArg is not VarRef faultsVr || !heap.IsWriter(faultsVr.Addr))
            return Abort(who, "Faults must be an unbound writer cell");

        // Capability gate (feature 050 US3, FR-008): verify-before-act — the scheme's gate runs
        // BEFORE any transport endpoint is opened or the link wired. A refusal has already been
        // RECORDED by the gate as a distinct outcome (ProvenanceOutcome.Refused); establishment
        // fails closed through the graceful Abort path — never a crash, never a silent drop
        // (FR-009). Schemes with no registered gate resolve to the allow-all default (loopback/tcp
        // unchanged).
        if (!link.CapabilityGates.Select(id.Scheme).GateEstablish(id))
            return Abort(who, $"capability refused for {id} — establishment fails closed (FR-008)");

        // Idempotency at link-identity (FR-007): re-establishment of the same ground
        // LinkId reuses the handle rather than opening a duplicate.
        bool firstEstablishment = !link.Links.Contains(id);
        LinkHandle handle;
        try
        {
            handle = link.Links.GetOrEstablish(id, () => new LinkHandle(
                id, establish(), LinkOptions.Default, link.PayloadCodecs.Select(id.Scheme)));
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or AggregateException)
        {
            return Abort(who, $"transport establishment failed for {id}: {Unwrap(ex).Message}");
        }

        if (!firstEstablishment)
            return Abort(who, $"re-establishment of already-established {id}: cell-aliasing unspecified (FR-007) — first only");

        // Wire the heap stream cursors onto the handle.
        handle.InWriterAddr = inVr.Addr;
        handle.OutReaderAddr = outVr.Addr;
        handle.FaultsWriterAddr = faultsVr.Addr;

        // Register the establishment `Faults` stream as a live monitor cursor (T034): a
        // transport fault fans out to it as well as to any later `link_monitor` stream
        // (FR-008). No `ok` is pushed here — the cell stays lazily unbound until a real
        // fault, so an unmonitored goal stays safely suspended (FR-044).
        LinkFaults.Register(handle, faultsVr.Addr);

        // Distributed-GC hook (T024/T035, FR-024): on close/permFail the reclaimer removes
        // this link's registry entry so the runtime returns to baseline. Other subsystems
        // (send-registry goals, reply-table) register their own hooks here when they gain live
        // per-link state; the base ground-relay path has only the registry entry today.
        link.Reclaimer.Register(id, () => link.Links.Remove(id));

        // Egress: observe binds on the writer the program fills as `Out` (egress rides the
        // ordinary drain, no pump needed). Ingress: start the background receive loop.
        int? outWriterAddr = heap.TryWriterForReader(outVr.Addr);
        if (outWriterAddr is null)
            return Abort(who, "Out reader has no paired writer to drain");
        ArmEgress(rt, link, handle, outWriterAddr.Value);
        link.Pump.AddLink(handle);
        rt.InboundPump ??= link.Pump;

        return BodyKernelResult.Success;
    }

    /// <summary>
    /// Arm the egress drainer: when the program binds the <c>Out</c> writer to a stream
    /// cons, ship the ground head (shared <see cref="LinkEgress.ShipGround"/>) and re-arm
    /// on the tail; an <c>Out = []</c> bind is the graceful stream-end close (FR-010).
    /// </summary>
    public static void ArmEgress(GlpRuntimeEngine rt, LinkRuntime link, LinkHandle handle, int outWriterAddr)
    {
        rt.Heap.OnBind(outWriterAddr, bound => OnOutboundBind(rt, link, handle, bound));
    }

    private static void OnOutboundBind(GlpRuntimeEngine rt, LinkRuntime link, LinkHandle handle, Term bound)
    {
        var heap = rt.Heap;
        var value = heap.Dereference(bound);

        // `Out = []` → graceful stream-end close (the program closed its sender). Runs the
        // SAME teardown as the abrupt '_link_close' kernel (T035) with reason `eos`: emit the
        // terminal closed(LinkId, eos) on the monitor, end it, close transport, run GC (FR-024).
        if (value is ConstTerm c && Equals(c.Value, Nil))
        {
            LinkTeardown.Teardown(rt, link, handle, LinkTerms.GracefulReason);
            return;
        }

        if (value is not StructTerm cons || cons.Functor != ListCons || cons.Args.Count != 2)
            return; // not a stream cons — nothing to ship (defensive; the wrapper only conses)

        // Ground-relay ship (FR-010), shared with the '_link_send'/3 kernel face. If a
        // non-ground head slips past the GLP `ground(Msg?)` guard, ShipGround throws and
        // the channel face drops the frame rather than placing a partial term on the wire.
        try
        {
            LinkEgress.ShipGround(heap, handle, cons.Args[0]);
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("[link egress] non-ground term reached Out — ground-relay gate violated; dropped");
            return;
        }

        // Re-arm on the tail's writer for the next value.
        var tail = heap.Dereference(cons.Args[1]);
        if (tail is VarRef tailVr)
        {
            int? tailWriter = heap.IsReader(tailVr.Addr)
                ? heap.TryWriterForReader(tailVr.Addr)
                : (heap.IsWriter(tailVr.Addr) ? tailVr.Addr : null);
            if (tailWriter is int tw)
                ArmEgress(rt, link, handle, tw);
        }
    }

    private static Exception Unwrap(Exception ex) =>
        ex is AggregateException agg && agg.InnerException is { } inner ? inner : ex;

    internal static BodyKernelResult Abort(string who, string why)
    {
        Console.WriteLine($"[ABORT] {who}: {why}");
        return BodyKernelResult.Abort;
    }
}
