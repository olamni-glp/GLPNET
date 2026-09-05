// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Term ordering acceptance (feature 102, T014-T018a).
// Covers SC-005, SC-012, SC-013, SC-015 and FR-015/FR-016/FR-018.
//
// This is the only part of the feature that is IRREVERSIBLE if got wrong: term ordering is
// monotone, so once boards fold no later operation can lower a winning term. Every test here is
// written so that deleting the thing it guards makes it FAIL — a test that still passes with its
// subject removed was never load-bearing.

using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Federation;
using Xunit;

namespace GlpRuntime.CrdtMsg.Tests.Federation;

public sealed class TermOrderingTests
{
    private const string LiveEpoch = "ynet-epoch-7f3a91c2e04b5d68";   // no wall clock: FR-026 applies to fixtures too
    private static readonly JsonElement EmptyBody = JsonSerializer.SerializeToElement(new { });

    private static TermSpaceRegistry Live() => new(LiveEpoch);

    private static FederationOp Op(string peer, long ctr, Term? term, string kind = "board_post") =>
        FederationOp.Create(new Dot(peer, ctr), peer, kind, EmptyBody, term);

    // ---- SC-005: a foreign-space term never wins, however large -------------------------------

    /// <summary>
    /// THE negative control for the whole ordering rule. A synthetic op in space "foreign" carrying
    /// long.MaxValue must not beat a live-space op carrying 1. A test that only checked the positive
    /// direction would still pass with the comparison deleted entirely.
    /// </summary>
    [Fact]
    public void ForeignSpaceMaximalTermNeverWins()
    {
        var live = new Term(LiveEpoch, 1, "gavriella");
        var foreign = new Term("foreign", long.MaxValue, "attacker");

        Assert.Equal(TermOrder.Incomparable, Term.Compare(foreign, live));
        Assert.Equal(TermOrder.Incomparable, Term.Compare(live, foreign));
        Assert.False(Term.Wins(foreign, live));   // the maximal term wins NOTHING
    }

    /// <summary>
    /// `Incomparable` is a THIRD VALUE, not a false. If this ever collapses to a boolean, a
    /// foreign-space term returning false reads as "the other one wins" — a decision made for a
    /// reason that is not a reason.
    /// </summary>
    [Fact]
    public void IncomparableIsAThirdResultNotABoolean()
    {
        var a = new Term("spaceA", 5, "h1");
        var b = new Term("spaceB", 5, "h1");

        var r = Term.Compare(a, b);
        Assert.Equal(TermOrder.Incomparable, r);
        Assert.NotEqual(TermOrder.Less, r);
        Assert.NotEqual(TermOrder.Equal, r);      // NOT equal either, though the numbers match
        Assert.NotEqual(TermOrder.Greater, r);
    }

    /// <summary>Within one space the ordering is ordinary and total.</summary>
    [Fact]
    public void WithinOneSpaceTermsOrderNormally()
    {
        var lo = new Term(LiveEpoch, 1, "h1");
        var hi = new Term(LiveEpoch, 2, "h1");
        Assert.Equal(TermOrder.Less, Term.Compare(lo, hi));
        Assert.Equal(TermOrder.Greater, Term.Compare(hi, lo));
        Assert.True(Term.Wins(hi, lo));
    }

    /// <summary>Equal counters tiebreak deterministically, so two replicas agree on the same winner.</summary>
    [Fact]
    public void EqualCountersTiebreakDeterministically()
    {
        var a = new Term(LiveEpoch, 7, "aaa");
        var b = new Term(LiveEpoch, 7, "bbb");
        Assert.Equal(TermOrder.Less, Term.Compare(a, b));
        Assert.Equal(TermOrder.Greater, Term.Compare(b, a));
        Assert.Equal(TermOrder.Equal, Term.Compare(a, a));
    }

    /// <summary>The live fossil, reproduced exactly: term 5961694 must not win in the live epoch.</summary>
    [Fact]
    public void TheFossilTermDoesNotWinAgainstALiveEpochTerm()
    {
        // op 628016928ab854ae carried 5961694 = floor(unix_ts/300), emitted by since-deleted code.
        var fossil = new Term(TermSpace.LegacyId, 5_961_694, "unknown-emitter");
        var legit = new Term(LiveEpoch, 1, "gavriella");

        Assert.Equal(TermOrder.Incomparable, Term.Compare(fossil, legit));
        Assert.False(Term.Wins(fossil, legit));
    }

    // ---- FR-015: the counter never moves with the clock ---------------------------------------

    /// <summary>
    /// A host offline for a week returns with the counter it left with. A wall-clock term advances
    /// fastest for the host that did the LEAST work — which is how the fossil outranks everything.
    /// </summary>
    [Fact]
    public void EraCounterDoesNotAdvanceWithElapsedTime()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-04T00:00:00Z"));
        var before = new Term(LiveEpoch, 3, "gavriella");

        clock.Advance(TimeSpan.FromDays(7));      // a week passes; no leadership event occurs

        var after = before;                        // nothing touched it, because nothing can
        Assert.Equal(3, after.EraCounter);
        Assert.Equal(TermOrder.Equal, Term.Compare(before, after));
    }

    /// <summary>The counter advances on a leadership EVENT, and only there.</summary>
    [Fact]
    public void EraCounterAdvancesOnlyOnALeadershipEvent()
    {
        var t = new Term(LiveEpoch, 3, "gavriella");
        var next = t.NextOnLeadershipEvent();
        Assert.Equal(4, next.EraCounter);
        Assert.Equal(t.SpaceId, next.SpaceId);
        Assert.True(Term.Wins(next, t));
    }

    // ---- SC-012: retirement retains AND excludes, both halves in ONE test ----------------------

    /// <summary>
    /// Both halves are asserted here together on purpose: split into two tests, one could be
    /// dropped and the remaining one would still look green while the property was gone.
    /// </summary>
    [Fact]
    public void RetiredOpRemainsInTheLogAndIsExcludedFromOrdering()
    {
        var fold = new FederationFold(Live());
        var fossilTerm = new Term(LiveEpoch, 5_961_694, "unknown-emitter");
        var fossil = Op("olamnit", 1, fossilTerm, "leader_claim");
        var legit = Op("gavriella", 1, new Term(LiveEpoch, 1, "gavriella"), "leader_claim");

        fold.Apply(fossil);
        fold.Apply(legit);

        // Before retirement the fossil DOES win — which is exactly why the remedy is needed.
        Assert.Equal(fossilTerm, fold.WinningTerm());

        fold.Apply(RetirementOp.Create(new Dot("gavriella", 2), "gavriella", fossil.OpId,
            "wall-clock-derived term from a deleted emitter; ruling Q-GLPNETG28-04"));

        // HALF 1 — still present. Removal is indistinguishable from suppression (FR-017).
        Assert.Contains(fold.Operations, o => o.OpId == fossil.OpId);
        Assert.True(fold.Contains(fossil.OpId));

        // HALF 2 — excluded from the ordering decision, and reported as unordered.
        Assert.Equal(OrderingDisposition.UnorderedLegacy, fold.DispositionOf(fossil));
        Assert.Equal(new Term(LiveEpoch, 1, "gavriella"), fold.WinningTerm());
        Assert.Contains(fold.Unordered(), x => x.Op.OpId == fossil.OpId);
    }

    /// <summary>Retiring an already-retired op is idempotent, not an error (I-19).</summary>
    [Fact]
    public void RetirementIsIdempotent()
    {
        var fold = new FederationFold(Live());
        var target = Op("olamnit", 1, new Term(LiveEpoch, 99, "olamnit"), "leader_claim");
        fold.Apply(target);

        fold.Apply(RetirementOp.Create(new Dot("g", 1), "g", target.OpId, "first"));
        fold.Apply(RetirementOp.Create(new Dot("g", 2), "g", target.OpId, "again"));

        Assert.Equal(OrderingDisposition.UnorderedLegacy, fold.DispositionOf(target));
        Assert.True(fold.Contains(target.OpId));
    }

    /// <summary>A retirement is an ORDINARY op: it folds and attributes like any other (FR-029).</summary>
    [Fact]
    public void ARetirementIsItselfAnOrdinaryBoardOperation()
    {
        var fold = new FederationFold(Live());
        var target = Op("olamnit", 1, null);
        var retire = RetirementOp.Create(new Dot("gavriella", 9), "gavriella", target.OpId, "because");

        fold.Apply(target);
        fold.Apply(retire);

        Assert.True(fold.Contains(retire.OpId));
        Assert.Equal("gavriella", retire.Origin);
        Assert.Equal(target.OpId, RetirementOp.TargetOf(retire));
        Assert.Equal("because", RetirementOp.ReasonOf(retire));
    }

    /// <summary>A retirement with no stated reason is refused — an unexplained retirement is unreviewable.</summary>
    [Fact]
    public void RetirementRequiresAStatedReason()
    {
        Assert.Throws<ArgumentException>(() =>
            RetirementOp.Create(new Dot("g", 1), "g", new Dot("o", 1), "   "));
    }

    // ---- SC-013: minting an epoch is additive -------------------------------------------------

    /// <summary>Prior-epoch operations stay readable and attributed after a new epoch is minted.</summary>
    [Fact]
    public void MintingANewEpochLeavesPriorEpochOpsReadableAndAttributed()
    {
        var priorOps = new[]
        {
            Op("gavriella", 1, new Term("ynet-epoch-2026-08", 1, "gavriella")),
            Op("olamnit",   1, new Term("ynet-epoch-2026-08", 2, "olamnit")),
        };

        var afterMint = new FederationFold(new TermSpaceRegistry("ynet-epoch-7f3a91c2e04b5d68"));
        afterMint.ApplyAll(priorOps);

        // Readable and attributed - nothing was rewritten by the mint.
        Assert.Equal(2, afterMint.Count);
        Assert.Contains(afterMint.Operations, o => o.Origin == "gavriella");
        Assert.Contains(afterMint.Operations, o => o.Origin == "olamnit");

        // ...and correctly no longer orderable, being from a space this host no longer runs.
        foreach (var o in afterMint.Operations)
            Assert.Equal(OrderingDisposition.UnorderedUnknownSpace, afterMint.DispositionOf(o));
    }

    /// <summary>An epoch id may not be clock-derived — that is how the fossil was born.</summary>
    [Fact]
    public void AClockDerivedEpochIdIsRefused()
    {
        Assert.True(TermSpaceRegistry.LooksClockDerived("5961694"));
        Assert.False(TermSpaceRegistry.LooksClockDerived("ynet-epoch-7f3a91c2e04b5d68"));
    }

    /// <summary>An empty epoch id is refused — an unminted space cannot order anything.</summary>
    [Fact]
    public void AnEmptyEpochIdIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new TermSpaceRegistry(""));
        Assert.Throws<ArgumentException>(() => new TermSpaceRegistry(TermSpace.LegacyId));
    }

    // ---- SC-015 / FR-016 / FR-031: THREE dispositions, never two ------------------------------

    /// <summary>
    /// Added by the analyze pass (finding C1). Unrecognised space, legacy space, and no-term-at-all
    /// must produce THREE DIFFERENT results. If any two collapse, an op from a space we have never
    /// heard of starts looking like a deliberately-retired one — a different fact with a different
    /// remedy — and that is the same two-states-one-output defect SC-007 forbids, one layer down.
    /// </summary>
    [Fact]
    public void UnknownSpaceLegacySpaceAndNoTermAreThreeDifferentResults()
    {
        var fold = new FederationFold(Live());

        var live = Op("gavriella", 1, new Term(LiveEpoch, 1, "gavriella"), "leader_claim");
        var unknownSpace = Op("stranger", 1, new Term("some-other-epoch", 42, "stranger"), "leader_claim");
        var legacySpace = Op("ancient", 1, new Term(TermSpace.LegacyId, 5_961_694, "ancient"), "leader_claim");
        var noTerm = Op("plain", 1, null);

        fold.ApplyAll(new[] { live, unknownSpace, legacySpace, noTerm });

        var dispositions = new[]
        {
            fold.DispositionOf(live),
            fold.DispositionOf(unknownSpace),
            fold.DispositionOf(legacySpace),
            fold.DispositionOf(noTerm),
        };

        Assert.Equal(OrderingDisposition.Orderable, dispositions[0]);
        Assert.Equal(OrderingDisposition.UnorderedUnknownSpace, dispositions[1]);
        Assert.Equal(OrderingDisposition.UnorderedLegacy, dispositions[2]);
        Assert.Equal(OrderingDisposition.NotLeadershipBearing, dispositions[3]);

        // The load-bearing assertion: all four are DISTINCT. This fails the moment any two collapse.
        Assert.Equal(4, dispositions.Distinct().Count());

        // And every one of them is RETAINED — none was dropped or coerced (FR-016 / FR-027).
        Assert.Equal(4, fold.Count);

        // Only the live one wins.
        Assert.Equal(new Term(LiveEpoch, 1, "gavriella"), fold.WinningTerm());
    }

    /// <summary>Classification itself distinguishes the three space kinds.</summary>
    [Fact]
    public void ClassifyDistinguishesLiveLegacyAndUnknown()
    {
        var reg = Live();
        Assert.Equal(SpaceKind.Live, reg.Classify(LiveEpoch).Kind);
        Assert.Equal(SpaceKind.Legacy, reg.Classify(null).Kind);
        Assert.Equal(SpaceKind.Legacy, reg.Classify("").Kind);
        Assert.Equal(SpaceKind.Legacy, reg.Classify(TermSpace.LegacyId).Kind);
        Assert.Equal(SpaceKind.Unknown, reg.Classify("who-knows").Kind);

        Assert.True(reg.IsOrderable(reg.LiveEpoch));
        Assert.False(reg.IsOrderable(TermSpace.Legacy));
        Assert.False(reg.IsOrderable(reg.Classify("who-knows")));
    }

    // ---- FR-018: the merge gate is load-bearing ------------------------------------------------

    /// <summary>A peer advertising no term-space capability is REFUSED. Deleting the gate fails this.</summary>
    [Fact]
    public void MergeIsRefusedWhenThePeerIsNotTermSpaceAware()
    {
        var v = MergeGate.CanMerge(new PeerCapabilities(TermSpaceAware: false, LiveEpoch), localTermSpaceAware: true);
        Assert.False(v.Allowed);
        Assert.Contains("not term-space aware", v.Reason);   // a SPECIFIC reason, not "merge failed"
    }

    /// <summary>A host that cannot confirm its OWN awareness refuses too — assuming is how a gate dies.</summary>
    [Fact]
    public void MergeIsRefusedWhenLocalAwarenessIsUnconfirmed()
    {
        var v = MergeGate.CanMerge(new PeerCapabilities(TermSpaceAware: true, LiveEpoch), localTermSpaceAware: false);
        Assert.False(v.Allowed);
        Assert.Contains("local term-space support unconfirmed", v.Reason);
    }

    /// <summary>The positive control: both sides aware ⇒ allowed. Without this the gate could just always refuse.</summary>
    [Fact]
    public void MergeIsAllowedWhenBothSidesAreTermSpaceAware()
    {
        var v = MergeGate.CanMerge(new PeerCapabilities(TermSpaceAware: true, LiveEpoch), localTermSpaceAware: true);
        Assert.True(v.Allowed);
    }
}

/// <summary>A controllable clock, so "seven days pass" is a test and not a wait.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    public FakeTimeProvider(DateTimeOffset start) => _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}
