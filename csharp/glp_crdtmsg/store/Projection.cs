// Rebuildable projection (feature 041-crdtmsg-mvp, T023; per-box projection 048 T011).
//
// Contract C10 / data-model §9: live state is a rebuildable PROJECTION over the op-WAL; crash → replay
// → zero loss (SC-004). The replay order is a DETERMINISTIC linear extension of the causal DAG (Kahn's
// algorithm, ties broken by dot order), so two replicas holding the same op SET fold to identical state
// regardless of arrival order (the convergence backbone for SC-003). CRDT op-body semantics plug in via
// the supplied reducer; the store guarantees the deterministic order + the op set.
//
// 048 (bk-colab-yngenios-transport, T011 / net-new C1): the box is the unit of convergence — box state
// is projected by folding ONLY that box's ops (048 data-model §1; op-wal-schema "projection, not
// storage": list/read project box state by folding the journal for a box; HEAD is rebuildable by
// replay, SC-008 byte-identical).

using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Store;

public sealed class Projection<TState>
{
    private readonly Func<TState> _seed;
    private readonly Func<TState, Op, TState> _apply;

    public Projection(Func<TState> seed, Func<TState, Op, TState> apply)
    {
        _seed = seed;
        _apply = apply;
    }

    /// <summary>Fold the ops (in canonical causal order) from the seed state.</summary>
    public TState Rebuild(IReadOnlyCollection<Op> ops)
    {
        TState state = _seed();
        foreach (var op in CanonicalOrder(ops))
            state = _apply(state, op);
        return state;
    }

    /// <summary>
    /// Per-box projection (048 T011 / C1): fold ONLY the ops owned by <paramref name="box"/> — the
    /// box is the unit of convergence, so one box's state never depends on another box's ops.
    /// </summary>
    public TState RebuildBox(IReadOnlyCollection<Op> ops, string box) =>
        Rebuild(ops.Where(o => o.Box == box).ToList());

    /// <summary>The distinct boxes present in an op set, ordinal-sorted (deterministic enumeration).</summary>
    public static IReadOnlyList<string> Boxes(IReadOnlyCollection<Op> ops) =>
        ops.Select(o => o.Box).Distinct().OrderBy(b => b, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Partition an op set by box (048 T011): each box's ops in canonical causal order, ready to fold
    /// independently (per-box projection = per-box QUIC stream = per-box convergence unit).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<Op>> PartitionByBox(IReadOnlyCollection<Op> ops)
    {
        var byBox = new Dictionary<string, IReadOnlyList<Op>>(StringComparer.Ordinal);
        foreach (var group in ops.GroupBy(o => o.Box, StringComparer.Ordinal))
            byBox[group.Key] = CanonicalOrder(group.ToList());
        return byBox;
    }

    /// <summary>
    /// Deterministic linear extension of the causal DAG. Deps referencing ops NOT in this set are
    /// treated as already-satisfied (external / not-yet-received context). Among ops whose deps are all
    /// satisfied, the one with the smallest dot is emitted next — a total, replica-independent order.
    /// </summary>
    public static IReadOnlyList<Op> CanonicalOrder(IReadOnlyCollection<Op> ops)
    {
        var byDot = new Dictionary<Dot, Op>();
        foreach (var op in ops) byDot[op.Id] = op; // dedupe by dot

        // indegree = number of deps present in the set; dependents = reverse edges.
        var indegree = new Dictionary<Dot, int>();
        var dependents = new Dictionary<Dot, List<Dot>>();
        foreach (var op in byDot.Values)
        {
            int deg = 0;
            foreach (var dep in op.Deps)
            {
                if (!byDot.ContainsKey(dep)) continue; // external dep ⇒ already satisfied
                deg++;
                (dependents.TryGetValue(dep, out var list) ? list : dependents[dep] = new List<Dot>()).Add(op.Id);
            }
            indegree[op.Id] = deg;
        }

        // ready set ordered by dot (SortedSet<Dot> — Dot is IComparable)
        var ready = new SortedSet<Dot>();
        foreach (var (dot, deg) in indegree)
            if (deg == 0) ready.Add(dot);

        var order = new List<Op>(byDot.Count);
        while (ready.Count > 0)
        {
            Dot next = ready.Min;
            ready.Remove(next);
            order.Add(byDot[next]);
            if (dependents.TryGetValue(next, out var deps))
                foreach (var d in deps)
                    if (--indegree[d] == 0) ready.Add(d);
        }

        // A cycle among present ops would leave some unemitted. Ops are a hash-chained DAG (acyclic by
        // construction); a residual cycle is a corruption — append the remainder in dot order so state
        // is still deterministic rather than silently dropping ops.
        if (order.Count < byDot.Count)
            foreach (var op in byDot.Values.Where(o => order.All(e => !e.Id.Equals(o.Id))).OrderBy(o => o.Id))
                order.Add(op);

        return order;
    }
}
