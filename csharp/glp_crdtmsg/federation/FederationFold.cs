// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The fold: union-by-id, order-independent, append-only (feature 102, T010).
//
// Contract federation-wire.md W6 / data-model I-13..I-16 / FR-010, FR-011, FR-012, FR-016, FR-031.
//
// Reuses Crdt.VersionVector UNCHANGED for the already-seen test. Redelivery is CERTAIN on any link
// that can drop and retry, so a fold that has not been tested against deliberate redelivery is
// untested, not convergent.
//
// There is no Remove/Delete/Rewrite on this type and none may be added (FR-011 / FR-017).

using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>
/// The deterministic function from a set of operations to current board state.
/// Order-independent and duplicate-independent by construction.
/// </summary>
public sealed class FederationFold
{
    private readonly TermSpaceRegistry _spaces;
    private readonly Dictionary<Dot, FederationOp> _ops = new();
    private readonly HashSet<Dot> _retired = new();

    // A HOLE-PRESERVING frontier, not a VersionVector. A max-merge vector reports "seen 7" after
    // 5 and 7 arrive, so Contains(6) answers TRUE for an op that never arrived, the pull suppresses
    // it, and a transient loss becomes permanent divergence. See FederationFrontier.
    private FederationFrontier _seen = new();

    public FederationFold(TermSpaceRegistry spaces) => _spaces = spaces;

    /// <summary>Operations in the fold, in the deterministic dot order (peer ordinal, then counter).</summary>
    public IReadOnlyList<FederationOp> Operations =>
        _ops.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();

    /// <summary>How many distinct operations the fold holds.</summary>
    public int Count => _ops.Count;

    /// <summary>True if this op-id has already been folded — the idempotence test (FR-010).</summary>
    public bool Contains(Dot opId) => _ops.ContainsKey(opId);

    /// <summary>
    /// Fold one operation in. Returns true if it was NEW, false if it was a redelivery.
    /// A redelivery is a no-op, not an error: retrying links are correct behaviour.
    /// </summary>
    public bool Apply(FederationOp op)
    {
        if (_ops.ContainsKey(op.OpId))
            return false;                       // union-by-id: redelivery does NOT double-count

        // The retirement body is decoded BEFORE the op is inserted. Decoding first means a
        // malformed body cannot leave the fold half-mutated: either the op and its consequence both
        // land, or neither does. (TargetOf returns null rather than throwing on every malformed
        // shape — including a fractional or out-of-range counter — but ordering the work this way
        // does not depend on that promise holding.)
        Dot? retires = RetirementOp.IsRetirement(op) ? RetirementOp.TargetOf(op) : null;

        _ops[op.OpId] = op;
        _seen = _seen.With(op.OpId);

        // A retirement op is an ORDINARY op that also records an ordering consequence (FR-029).
        if (retires is { } target) _retired.Add(target);

        return true;
    }

    /// <summary>Fold a batch. Order of the batch is irrelevant to the result (FR-012).</summary>
    public int ApplyAll(IEnumerable<FederationOp> ops)
    {
        int added = 0;
        foreach (var op in ops) if (Apply(op)) added++;
        return added;
    }

    /// <summary>
    /// The ordering disposition of one operation. Four outcomes, all distinguishable (FR-031).
    /// A retired op reports as <see cref="OrderingDisposition.UnorderedLegacy"/> — because
    /// retirement assigns it to the legacy space — while REMAINING PRESENT in the fold (SC-012).
    /// </summary>
    public OrderingDisposition DispositionOf(FederationOp op)
    {
        if (op.Term is not { } term)
            return OrderingDisposition.NotLeadershipBearing;

        if (_retired.Contains(op.OpId))
            return OrderingDisposition.UnorderedLegacy;

        return _spaces.Classify(term.SpaceId).Kind switch
        {
            SpaceKind.Live => OrderingDisposition.Orderable,
            SpaceKind.Legacy => OrderingDisposition.UnorderedLegacy,
            _ => OrderingDisposition.UnorderedUnknownSpace,
        };
    }

    /// <summary>
    /// The winning leadership term, or null if no operation is orderable.
    /// Only <see cref="OrderingDisposition.Orderable"/> operations are candidates — which is what
    /// keeps the fossil, and any foreign-space op carrying long.MaxValue, out of the decision.
    /// </summary>
    public Term? WinningTerm()
    {
        Term? best = null;
        foreach (var op in Operations)
        {
            if (DispositionOf(op) != OrderingDisposition.Orderable) continue;
            var t = op.Term!.Value;
            if (best is null || Term.Wins(t, best.Value)) best = t;
        }
        return best;
    }

    /// <summary>
    /// Operations retained but excluded from ordering, with the reason. The status surface reports
    /// these rather than hiding them — a retained-unordered op is a fact an operator needs.
    /// </summary>
    public IReadOnlyList<(FederationOp Op, OrderingDisposition Why)> Unordered() =>
        Operations.Select(o => (o, DispositionOf(o)))
                  .Where(x => x.Item2 is OrderingDisposition.UnorderedLegacy
                                       or OrderingDisposition.UnorderedUnknownSpace)
                  .ToList();

    /// <summary>
    /// Byte-comparable canonical rendering of the whole fold. Two hosts holding the same op set
    /// MUST produce identical bytes regardless of arrival order (FR-012 / SC-003). Comparing folds
    /// through a bespoke "equivalent" comparer would hide exactly the bug that assertion exists for.
    /// </summary>
    public string ToCanonicalJson() =>
        "[" + string.Join(",", Operations.Select(o => o.ToCanonicalJson())) + "]";

    /// <summary>
    /// The causal frontier for the reconciliation pull (contract W5). Hole-preserving: a gap is
    /// advertised AS a gap, so the peer resends what was lost instead of suppressing it forever.
    /// </summary>
    public FederationFrontier Frontier => _seen;
}
