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
/// Two DIFFERENT operations claimed the same dot. Not a redelivery — a conflict, and one that would
/// otherwise make the fold arrival-order dependent (FR-012).
/// </summary>
public sealed class DotConflictException : InvalidOperationException
{
    public DotConflictException(Dot opId)
        : base($"two different operations claim dot {opId} — a dot identifies ONE operation; "
             + "accepting the second would make this fold depend on arrival order (FR-012)")
        => OpId = opId;

    public Dot OpId { get; }
}

/// <summary>
/// The deterministic function from a set of operations to current board state.
/// Order-independent and duplicate-independent by construction.
/// </summary>
public sealed class FederationFold
{
    private readonly TermSpaceRegistry _spaces;

    // EVERY access to the three fields below takes _gate.
    //
    // The receive loop, the pull loop and the board tail all fold concurrently, and a Dictionary
    // enumerated during a mutation THROWS. A faulted background loop stops converging silently,
    // which is precisely the class of failure this feature exists to remove — so the safety here
    // is not defensive padding, it is the convergence guarantee.
    private readonly object _gate = new();
    private readonly Dictionary<Dot, FederationOp> _ops = new();
    private readonly HashSet<Dot> _retired = new();

    // A HOLE-PRESERVING frontier, not a VersionVector. A max-merge vector reports "seen 7" after
    // 5 and 7 arrive, so Contains(6) answers TRUE for an op that never arrived, the pull suppresses
    // it, and a transient loss becomes permanent divergence. See FederationFrontier.
    private FederationFrontier _seen = new();

    public FederationFold(TermSpaceRegistry spaces) => _spaces = spaces;

    /// <summary>Operations in the fold, in the deterministic dot order (peer ordinal, then counter).</summary>
    public IReadOnlyList<FederationOp> Operations
    {
        // Materialised UNDER the lock: returning a lazy query would enumerate the dictionary later,
        // outside it, which is the same race in a slower costume.
        get { lock (_gate) return _ops.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList(); }
    }

    /// <summary>How many distinct operations the fold holds.</summary>
    public int Count { get { lock (_gate) return _ops.Count; } }

    /// <summary>True if this op-id has already been folded — the idempotence test (FR-010).</summary>
    public bool Contains(Dot opId) { lock (_gate) return _ops.ContainsKey(opId); }

    /// <summary>
    /// Fold one operation in. Returns true if it was NEW, false if it was a redelivery.
    /// A redelivery is a no-op, not an error: retrying links are correct behaviour.
    /// </summary>
    public bool Apply(FederationOp op)
    {
        lock (_gate) return ApplyLocked(op);
    }

    private bool ApplyLocked(FederationOp op)
    {
        // THE FOLD BOUNDARY IS THE LAST LINE OF DEFENCE. FederationOp is a record with init-only
        // properties, so a caller can bypass BOTH guarded factories with an object initialiser. A
        // nonpositive counter reaching here is unrecoverable: FederationFrontier's contiguous run
        // starts at 0, so it reports the dot as already covered and a lost push can never be
        // repaired. Guarding only the constructors left a door open that the type system does not.
        if (op.OpId.Counter < 1)
            throw new ArgumentOutOfRangeException(nameof(op),
                $"dot counter must be >= 1; got {op.OpId.Counter}. Every frontier reports a "
                + "nonpositive counter as already-held, so this operation could never be reconciled.");

        if (_ops.TryGetValue(op.OpId, out var existing))
        {
            // A REDELIVERY IS THE SAME OPERATION. Two DIFFERENT operations sharing a dot are not a
            // redelivery, they are a conflict — and treating them as one made the fold
            // arrival-order dependent: whichever landed first won, so two replicas that received
            // them in opposite orders held different values forever while both reported converged.
            // That is exactly the FR-012 property this type exists to guarantee.
            //
            // Compared on CANONICAL BYTES, the same comparison SC-003 asserts folds by.
            if (!string.Equals(existing.ToCanonicalJson(), op.ToCanonicalJson(), StringComparison.Ordinal))
                throw new DotConflictException(op.OpId);

            return false;                       // union-by-id: redelivery does NOT double-count
        }

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
        // ONE lock for the whole batch, so a concurrent reader never observes a half-applied batch.
        lock (_gate) foreach (var op in ops) if (ApplyLocked(op)) added++;
        return added;
    }

    /// <summary>
    /// The ordering disposition of one operation. Four outcomes, all distinguishable (FR-031).
    /// A retired op reports as <see cref="OrderingDisposition.UnorderedLegacy"/> — because
    /// retirement assigns it to the legacy space — while REMAINING PRESENT in the fold (SC-012).
    /// </summary>
    public OrderingDisposition DispositionOf(FederationOp op)
    {
        lock (_gate) return DispositionLocked(op);
    }

    private OrderingDisposition DispositionLocked(FederationOp op)
    {
        // RETIREMENT IS CHECKED FIRST. Asking about the term first meant a retired op carrying no
        // term — an ordinary board post, or another retirement — reported NotLeadershipBearing and
        // its retirement was invisible, contradicting SC-012 and making "a retirement is itself
        // retirable" (FR-029) unobservable.
        if (_retired.Contains(op.OpId))
            return OrderingDisposition.UnorderedLegacy;

        if (op.Term is not { } term)
            return OrderingDisposition.NotLeadershipBearing;

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
            if (DispositionLockedSafe(op) != OrderingDisposition.Orderable) continue;
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
        Operations.Select(o => (o, DispositionLockedSafe(o)))
                  .Where(x => x.Item2 is OrderingDisposition.UnorderedLegacy
                                       or OrderingDisposition.UnorderedUnknownSpace)
                  .ToList();

    /// <summary>
    /// Byte-comparable canonical rendering of the whole fold. Two hosts holding the same op set
    /// MUST produce identical bytes regardless of arrival order (FR-012 / SC-003). Comparing folds
    /// through a bespoke "equivalent" comparer would hide exactly the bug that assertion exists for.
    /// </summary>
    public string ToCanonicalJson()
    {
        lock (_gate)
            return "[" + string.Join(",",
                _ops.OrderBy(kv => kv.Key).Select(kv => kv.Value.ToCanonicalJson())) + "]";
    }

    /// <summary>
    /// Disposition for callers iterating a materialised snapshot. Takes the lock per call rather
    /// than holding it across the whole iteration, so a long fold never blocks the receive loop.
    /// </summary>
    private OrderingDisposition DispositionLockedSafe(FederationOp op)
    {
        lock (_gate) return DispositionLocked(op);
    }

    /// <summary>
    /// How this fold classifies a term-space.
    /// <para>
    /// Exposed so admission can tell a LIVE term — which can become the permanent winner, and so
    /// needs a verified origin — from a legacy or unknown one, which is incomparable by construction
    /// and must simply be RETAINED (FR-016, FR-027). Without the distinction, protecting the former
    /// deleted the latter.
    /// </para>
    /// </summary>
    public SpaceKind SpaceKindOf(string spaceId)
    {
        lock (_gate) return _spaces.Classify(spaceId).Kind;
    }

    /// <summary>
    /// The causal frontier for the reconciliation pull (contract W5). Hole-preserving: a gap is
    /// advertised AS a gap, so the peer resends what was lost instead of suppressing it forever.
    /// </summary>
    public FederationFrontier Frontier { get { lock (_gate) return _seen; } }
}
