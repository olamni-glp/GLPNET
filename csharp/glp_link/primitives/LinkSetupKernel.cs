using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The <c>'_link_setup'/5</c> body kernel (feature 025, T030) — the host body of the
/// GLP wrapper
/// <c>link_setup(LinkId, Role, ch(In?, Out), Faults?) :- ground(LinkId?), ground(Role?) | '_link_setup'(LinkId?, Role?, In, Out?, Faults).</c>
/// (contracts/link-primitives.md §2.1). It establishes-or-reuses a transport leaf
/// for a ground <see cref="LinkId"/> in a given <see cref="LinkRole"/>, wires the
/// three heap stream cursors (In writer / Out reader / Faults writer), arms the
/// egress drainer on <c>Out</c>, starts the ingress pump, and registers the handle
/// idempotently (FR-001/002/003/004/007).
/// </summary>
/// <remarks>
/// Arguments (already ground-guarded by the GLP wrapper, so a malformed/unbound term
/// here is a caller bug — surfaced as <see cref="BodyKernelResult.Abort"/>, never
/// tolerated):
/// <list type="number">
///   <item><c>args[0]</c> LinkId — ground <c>link_id(Scheme, Endpoint, Nonce)</c>.</item>
///   <item><c>args[1]</c> Role — ground <c>listener</c>|<c>connector</c>.</item>
///   <item><c>args[2]</c> In — the body WRITER the host extends as inbound frames arrive (program reads <c>In?</c>).</item>
///   <item><c>args[3]</c> Out? — the body READER the host drains as the program writes <c>Out</c>.</item>
///   <item><c>args[4]</c> Faults — the body WRITER the host extends with fault terms (program reads <c>Faults?</c>).</item>
/// </list>
/// </remarks>
public static class LinkSetupKernel
{
    private const string ListCons = ".";
    private const string Nil = "nil";

    public static BodyKernelResult LinkSetup(GlpRuntimeEngine rt, IReadOnlyList<object?> args, LinkRuntime link)
    {
        if (args.Count != 5)
            return Abort($"expected 5 arguments, got {args.Count}");

        var heap = rt.Heap;

        // --- parse the two ground inputs (LinkId, Role) ---
        LinkId id;
        LinkRole role;
        try
        {
            id = LinkTerms.ParseLinkId(heap.Dereference((Term)args[0]!));
            role = LinkTerms.ParseRole(heap.Dereference((Term)args[1]!));
        }
        catch (ArgumentException ex)
        {
            return Abort(ex.Message);
        }

        // --- resolve the three output cells to their RAW heap addresses ---
        // Use the bare arg VarRef address, NOT heap.Dereference: a local reader
        // dereferences to its paired WRITER (DerefAddr canonicalizes reader→writer),
        // which would erase the Out? reader identity. The arg slots are the direct
        // cell references the wrapper head minted.
        if (args[2] is not VarRef inVr || !heap.IsWriter(inVr.Addr))
            return Abort("arg 3 (In) must be an unbound writer cell");
        if (args[3] is not VarRef outVr || !heap.IsReader(outVr.Addr))
            return Abort("arg 4 (Out?) must be an unbound reader cell");
        if (args[4] is not VarRef faultsVr || !heap.IsWriter(faultsVr.Addr))
            return Abort("arg 5 (Faults) must be an unbound writer cell");

        // --- idempotency at link-identity (FR-007): a re-setup with the same ground
        //     LinkId reuses the established handle rather than opening a duplicate.
        bool firstEstablishment = !link.Links.Contains(id);
        LinkHandle handle;
        try
        {
            handle = link.Links.GetOrEstablish(id, () => Establish(link, id, role));
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or AggregateException)
        {
            return Abort($"transport establishment failed for {id}: {Unwrap(ex).Message}");
        }

        if (!firstEstablishment)
        {
            // The handle already exists. Re-binding the NEW In/Out/Faults holes to the
            // existing link is the FR-007 re-setup cell-aliasing case, which the rulings
            // log leaves open (project_025 WIP note). Surface it rather than guess.
            return Abort($"re-setup of already-established {id}: cell-aliasing unspecified (FR-007) — first-establishment only");
        }

        // --- wire the heap stream cursors onto the handle ---
        handle.InWriterAddr = inVr.Addr;
        handle.OutReaderAddr = outVr.Addr;
        handle.FaultsWriterAddr = faultsVr.Addr;

        // --- egress: observe binds on the writer the program fills as `Out` (design
        //     ref §1.4 — egress rides the ordinary drain, no pump needed). ---
        int? outWriterAddr = heap.TryWriterForReader(outVr.Addr);
        if (outWriterAddr is null)
            return Abort("Out reader has no paired writer to drain");
        ArmEgress(heap, handle, outWriterAddr.Value);

        // --- ingress: start the background receive loop feeding the pump's inbox. ---
        link.Pump.AddLink(handle);

        // --- install the pump so the engine's run-to-quiescence driver services it
        //     (idempotent: the first setup on this engine installs the one pump). ---
        rt.InboundPump ??= link.Pump;

        return BodyKernelResult.Success;
    }

    /// <summary>Open the transport leaf for <paramref name="id"/> in <paramref name="role"/> and build the handle.</summary>
    private static LinkHandle Establish(LinkRuntime link, LinkId id, LinkRole role)
    {
        var transport = link.Transports.Select(id.Scheme);
        var opts = LinkOptions.Default;
        // The establishment rendezvous blocks the runner thread until the peer appears
        // (you genuinely cannot proceed until connected); the peer runs in another
        // engine/process, so this is not a self-deadlock. Bounded by ConnectTimeout.
        using var cts = new CancellationTokenSource(opts.ConnectTimeout);
        var endpointTask = role == LinkRole.Listener
            ? transport.ListenAsync(id.Scheme, id.Endpoint, opts, cts.Token)
            : transport.ConnectAsync(id.Scheme, id.Endpoint, opts, cts.Token);
        var endpoint = endpointTask.GetAwaiter().GetResult();
        // The handle keeps the GLP-supplied ground LinkId as its identity (FR-007 key),
        // not the transport-minted endpoint id — both establishment paths converge here.
        return new LinkHandle(id, endpoint, opts);
    }

    /// <summary>
    /// Arm the egress drainer: when the program binds the <c>Out</c> writer to a
    /// stream cons, ship the ground head and re-arm on the tail (FR-010 ground-relay).
    /// </summary>
    private static void ArmEgress(HeapFCP heap, LinkHandle handle, int outWriterAddr)
    {
        heap.OnBind(outWriterAddr, bound => OnOutboundBind(heap, handle, bound));
    }

    private static void OnOutboundBind(HeapFCP heap, LinkHandle handle, Term bound)
    {
        var value = heap.Dereference(bound);

        // `Out = []` → graceful stream-end close (the program closed its sender).
        if (value is ConstTerm c && Equals(c.Value, Nil))
        {
            handle.Endpoint.CloseAsync().GetAwaiter().GetResult();
            return;
        }

        if (value is not StructTerm cons || cons.Functor != ListCons || cons.Args.Count != 2)
            return; // not a stream cons — nothing to ship (defensive; the wrapper only conses)

        // Ground-relay ship (FR-010), shared with the '_link_send'/3 kernel face. The
        // ground gate is the GLP `ground(Msg?)` guard; if a non-ground head slips past
        // it onto the stream, ShipGround throws here — the channel face drops the frame
        // rather than placing a partial term on the wire (the kernel face Aborts).
        try
        {
            LinkEgress.ShipGround(heap, handle, cons.Args[0]);
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("[link egress] non-ground term reached Out — ground-relay gate violated; dropped");
            return;
        }

        // Re-arm on the tail's writer for the next value (the `Out?` reader of the cons
        // is paired with the writer the next link_send fills).
        var tail = heap.Dereference(cons.Args[1]);
        if (tail is VarRef tailVr)
        {
            int? tailWriter = heap.IsReader(tailVr.Addr)
                ? heap.TryWriterForReader(tailVr.Addr)
                : (heap.IsWriter(tailVr.Addr) ? tailVr.Addr : null);
            if (tailWriter is int tw)
                ArmEgress(heap, handle, tw);
        }
    }

    private static Exception Unwrap(Exception ex) =>
        ex is AggregateException agg && agg.InnerException is { } inner ? inner : ex;

    private static BodyKernelResult Abort(string why)
    {
        Console.WriteLine($"[ABORT] '_link_setup'/5: {why}");
        return BodyKernelResult.Abort;
    }
}
