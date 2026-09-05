// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The namespaced term and its THREE-VALUED comparison (feature 102, T005/T013).
//
// Contract term-ordering.md C1/C2/C3 / data-model I-9..I-12 / FR-013, FR-014, FR-015.
// Authority: engineer ruling Q-GLPNETG27-03 — term := (space_id, era_counter, host_id), STOP ORDER.
//
// THE ONE THING TO UNDERSTAND HERE: `Incomparable` is a FIRST-CLASS THIRD RESULT, not `false`.
// If this compared as a bool ("does a beat b"), a foreign-space term returning false would read as
// "b wins" — and the caller would then hand the decision to b for a reason that is not a reason.
// Term ordering is MONOTONE: once boards fold, nothing can lower a winning term. A collapse here is
// not a bug you fix later; it is a board you cannot un-poison.

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>The four possible outcomes of comparing two terms. Four, not three — see I-11.</summary>
public enum TermOrder
{
    Less,
    Equal,
    Greater,

    /// <summary>
    /// The terms belong to different spaces and are NOT ordered relative to one another by
    /// magnitude (FR-014). This is a result, not a failure, and MUST NOT be folded into a boolean.
    /// </summary>
    Incomparable,
}

/// <summary>
/// A leadership-bearing ordering value: (space, era counter, originating participant).
/// Always the full triple — there is deliberately no constructor taking a bare number, because a
/// bare number is exactly what the fossil op is.
/// </summary>
public readonly record struct Term(string SpaceId, long EraCounter, string HostId)
{
    /// <summary>
    /// Compare two terms. Returns <see cref="TermOrder.Incomparable"/> across spaces — always,
    /// unconditionally, whatever the counters say (FR-014, SC-005).
    /// </summary>
    public static TermOrder Compare(Term a, Term b)
    {
        // Cross-space FIRST, before any magnitude is even looked at. A synthetic op carrying
        // long.MaxValue in space "foreign" must lose to a live-space op carrying 1.
        if (!string.Equals(a.SpaceId, b.SpaceId, StringComparison.Ordinal))
            return TermOrder.Incomparable;

        int c = a.EraCounter.CompareTo(b.EraCounter);
        if (c < 0) return TermOrder.Less;
        if (c > 0) return TermOrder.Greater;

        // Same space, same counter: a DETERMINISTIC TIEBREAK so two replicas agree. This is not a
        // claim that one host outranks another — it is only that both hosts pick the same one.
        int h = string.CompareOrdinal(a.HostId, b.HostId);
        return h < 0 ? TermOrder.Less : h > 0 ? TermOrder.Greater : TermOrder.Equal;
    }

    /// <summary>
    /// The ONLY boolean permitted over terms. Safe precisely because
    /// <see cref="TermOrder.Incomparable"/> also yields false: an incomparable term never wins,
    /// which is the conservative direction.
    /// </summary>
    public static bool Wins(Term a, Term b) => Compare(a, b) == TermOrder.Greater;

    /// <summary>
    /// The successor term for a LEADERSHIP EVENT. This is the only way a counter advances (FR-015).
    /// There is no overload taking a clock, a tick, a timestamp or an interval, and none may be
    /// added: a wall-clock term advances fastest for the host that did the least work, and a host
    /// switched off for a week would return with an ordering advantage it did not earn.
    /// </summary>
    public Term NextOnLeadershipEvent() => this with { EraCounter = EraCounter + 1 };

    public override string ToString() => $"({SpaceId}, {EraCounter}, {HostId})";
}
