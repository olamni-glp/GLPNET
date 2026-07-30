using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The restore-time re-wire path (feature 061 US4, DEF-E1): adopt a link restored
/// from a snapshot whose In/Out/Faults cells are ALREADY IN THE HEAP and possibly
/// bound — the state <see cref="LinkEstablish.WireEstablishedLink"/> rightly aborts
/// on for the normal establishment path (its unbound-cell guards stay untouched;
/// research.md D9 rejected relaxing them). Registers the handle idempotently via
/// <see cref="LinkRegistry.GetOrEstablish"/> (the same registry both normal paths
/// converge on), wires the cursors at their RESTORED positions, arms the egress
/// drainer at the first unshipped stream position, and starts the ingress pump.
/// </summary>
/// <remarks>
/// Runner-thread only: adoption binds nothing but registers heap OnBind callbacks
/// and pump cursors, so it must not race a running reduction. The at-most-once
/// crash boundary (FR-032) is enforced by the egress walk: every stream element
/// bound BEFORE the snapshot was shipped at bind time (the drainer ships
/// synchronously on bind), so the drainer re-arms at the first UNBOUND tail —
/// committed work is never re-shipped (no duplication) and every post-restore
/// bind ships (no loss).
/// </remarks>
public static class RewireHandle
{
    private const string ListCons = ".";
    private const string Nil = "nil";

    /// <summary>
    /// Adopt the restored link <paramref name="id"/>: establish (or reuse) its
    /// transport endpoint via <paramref name="establish"/>, wire the restored
    /// cursor addresses, and resume ingress/egress. Re-adoption of an
    /// already-registered id returns the existing handle unchanged (idempotent at
    /// link-identity, FR-007 — no double wiring, no duplicate pump registration).
    /// Throws <see cref="InvalidOperationException"/> on a cursor address that is
    /// not the expected cell kind — a corrupt snapshot, surfaced loudly.
    /// </summary>
    public static LinkHandle Adopt(
        GlpRuntimeEngine rt, LinkRuntime link, LinkId id, LinkRole role,
        Func<ILinkEndpoint> establish,
        int? inWriterAddr, int? outReaderAddr, int? faultsWriterAddr,
        IReadOnlyList<int> monitorCursors)
    {
        var heap = rt.Heap;

        // Loud cursor validation — pre-BOUND is expected (that is the point of this
        // path); a wrong CELL KIND at a restored address is corrupt snapshot data.
        RequireKind(heap, inWriterAddr, heap.IsWriter, "In cursor", "writer");
        RequireKind(heap, outReaderAddr, heap.IsReader, "Out cursor", "reader");
        RequireKind(heap, faultsWriterAddr, heap.IsWriter, "Faults cursor", "writer");
        foreach (var c in monitorCursors)
            RequireKind(heap, c, heap.IsWriter, "monitor cursor", "writer");

        bool firstAdoption = !link.Links.Contains(id);
        var handle = link.Links.GetOrEstablish(id, () => new LinkHandle(
            id, establish(), LinkOptions.Default, link.PayloadCodecs.Select(id.Scheme))
        { Role = role });
        if (!firstAdoption)
            return handle; // idempotent re-adoption — already wired

        // Cursors resume at their restored positions (T031). The restored
        // MonitorCursors list already contains the establishment Faults cursor at
        // its CURRENT stream position — re-registering via LinkFaults.Register here
        // would duplicate fault delivery.
        handle.InWriterAddr = inWriterAddr;
        handle.OutReaderAddr = outReaderAddr;
        handle.FaultsWriterAddr = faultsWriterAddr;
        handle.MonitorCursors.AddRange(monitorCursors);

        // Distributed-GC hook — same registration the normal establish path makes.
        link.Reclaimer.Register(id, () => link.Links.Remove(id));

        // Egress: re-arm at the first UNSHIPPED (unbound) tail of the restored Out
        // stream, never on a bound cell (heap.OnBind fires IMMEDIATELY on a bound
        // writer, which would re-ship committed work — FR-032 forbids duplication).
        if (outReaderAddr is int ora && UnshippedTailWriter(heap, ora) is int tailWriter)
            LinkEstablish.ArmEgress(rt, link, handle, tailWriter);

        // Ingress: resume the pump at the restored In tail. A null In cursor means
        // the peer had already ended the stream pre-snapshot — nothing to pump.
        if (inWriterAddr is not null)
        {
            link.Pump.AddLink(handle);
            rt.InboundPump ??= link.Pump;
        }

        return handle;
    }

    /// <summary>
    /// Walk the restored Out stream from its original reader to the first UNBOUND
    /// tail writer — the resume point for the egress drainer. Null when there is
    /// nothing to arm (stream closed with <c>[]</c>, or not a stream shape).
    /// </summary>
    internal static int? UnshippedTailWriter(HeapFCP heap, int outReaderAddr)
    {
        Term cursor = new VarRef(outReaderAddr);
        // Bounded by the heap size: each step either lands on an unbound writer
        // (return), moves to a strictly-later stream tail, or exits.
        for (int guard = 0; guard <= heap.Hp; guard++)
        {
            if (cursor is VarRef vr)
            {
                int? writer = heap.IsWriter(vr.Addr) ? vr.Addr : heap.TryWriterForReader(vr.Addr);
                if (writer is not int w)
                    return null; // unpaired reader — nothing drainable
                if (!heap.IsFullyBound(w))
                    return w;    // the first unshipped position
                cursor = heap.Dereference(new VarRef(w));
                continue;
            }
            if (cursor is StructTerm { Functor: ListCons } cons && cons.Args.Count == 2)
            {
                cursor = cons.Args[1]; // already-shipped element — skip to the tail
                continue;
            }
            return null; // [] (closed stream) or a non-stream value — nothing to arm
        }
        throw new InvalidOperationException(
            $"restored Out stream from reader {outReaderAddr} does not terminate — cyclic snapshot data");
    }

    private static void RequireKind(
        HeapFCP heap, int? addr, Func<int, bool> isKind, string what, string kind)
    {
        if (addr is not int a)
            return;
        if (a < 0 || a >= heap.Hp || !isKind(a))
            throw new InvalidOperationException(
                $"restored {what} at address {a} is not a {kind} cell — corrupt snapshot");
    }
}
