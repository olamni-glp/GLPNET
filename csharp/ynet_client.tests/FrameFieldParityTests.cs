// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Net;
using System.Text;
using Xunit.Abstractions;
using Ynet.Client;
using Ynet.Transport.Capability;

namespace Ynet.Client.Tests;

/// <summary>
/// Feature 110 — <b>carrier</b> frame-field parity.
///
/// <para>
/// 🔴 <b>This is NOT the same property as <see cref="FrameParityTests"/>.</b> That class (era 107)
/// compares two SERIALIZATIONS of ONE hand-built frame, and so proves the two ENCODERS agree. This
/// class drives the two CARRIERS and compares what each one PUTS IN the frame. Era 107's class doc
/// promised the property this class tests; the test could not reach it, because neither carrier
/// offered a seam and the frame was constructed inline inside <c>Send</c>. Feature 110 added the
/// seam. Both classes are kept: they test real and different things.
/// </para>
///
/// <para>
/// <b>Six findings from an adversarial review are answered in this file</b>, three of them HIGH.
/// The corrections are marked at the tests that carry them, because the reasoning is the valuable
/// part and a silent fix teaches nobody.
/// </para>
/// </summary>
public class FrameFieldParityTests : IDisposable
{
    private readonly string _root;
    private readonly ITestOutputHelper _out;

    public FrameFieldParityTests(ITestOutputHelper output)
    {
        _out = output;
        _root = Path.Combine(Path.GetTempPath(), "ynet-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp dir cleanup is best-effort */ }
        GC.SuppressFinalize(this);
    }

    private const string Signal = "M6_MESSAGE";
    private const string Body = "the quick brown fox";

    private static readonly PeerIdentity Self = new("nodeA", "laneA");
    private static readonly PeerIdentity Peer = new("nodeB", "laneB");

    /// <summary>
    /// The FIRST frame each carrier constructs for one identical logical send.
    ///
    /// <para>
    /// 🔴 <b>SCOPE, per review finding 5.</b> This drives <c>BuildFrame</c> directly, so it
    /// deliberately bypasses each carrier's send preconditions. That is correct for comparing
    /// CONSTRUCTION and it is NOT a claim about two long-running carriers: the file plane
    /// increments only after a reachability check, while the wire plane increments before size and
    /// connection validation, so in service the two counters drift and absolute equality is
    /// meaningless. What is compared here is the BASIS (does the first frame number 1?), which is
    /// the property Q-110-02 actually ruled.
    /// </para>
    /// </summary>
    private (YnetFrame File, YnetFrame Wire) ConstructedPair(string fileBody = Body, string wireBody = Body)
    {
        var file = new CoopFileOutbound(Self, Peer, _root);

        using var selfNode = NodeIdentity.Generate();
        using var peerNode = NodeIdentity.Generate();
        using var wire = new QuicOutbound(
            selfNode, peerNode.NodeId, Peer, new IPEndPoint(IPAddress.Loopback, 47999));

        var message = new YnetMessage("mid-110", "nodeA/laneA", Signal, Encoding.UTF8.GetBytes(wireBody));

        return (file.BuildFrame(Signal, fileBody), wire.BuildFrame(message));
    }

    /// <summary>
    /// THE GUARD, as its own fact so it cannot be skipped or reordered away. Two empty frames also
    /// compare equal — wave-33 measured exactly that trap, where a cross-runtime agreement criterion
    /// compared two transcripts that were both empty and reported agreement.
    /// </summary>
    [Fact]
    public void Both_carriers_construct_a_non_empty_frame()
    {
        var (file, wire) = ConstructedPair();

        Assert.False(string.IsNullOrEmpty(file.Origin));
        Assert.False(string.IsNullOrEmpty(file.SenderActor));
        Assert.False(string.IsNullOrEmpty(wire.Origin));
        Assert.False(string.IsNullOrEmpty(wire.SenderActor));
    }

    /// <summary>
    /// The acceptance gate. 🔴 <b>Review finding 1</b>: this now PRINTS the full report on the
    /// GREEN path. Previously the report appeared only inside a failure message, so a passing run
    /// showed nothing and every permitted divergence was invisible — which is precisely the
    /// suppression FR-006 exists to prevent. A ruled divergence you never see is not a declared
    /// decision, it is a hidden one.
    /// </summary>
    [Fact]
    public void Every_field_is_either_agreed_or_ruled_never_unruled()
    {
        var (file, wire) = ConstructedPair();
        var comparisons = FrameFieldParity.Compare(file, wire);

        _out.WriteLine("Carrier frame-field parity — every field, pass or fail:");
        _out.WriteLine(FrameFieldParity.Report(comparisons));

        Assert.True(
            FrameFieldParity.Accepts(comparisons),
            "Unruled divergence between the two carriers:" + Environment.NewLine
            + FrameFieldParity.Report(comparisons));
    }

    /// <summary>
    /// FR-007 — the check ENUMERATES the envelope rather than trusting a hand list.
    ///
    /// <para>
    /// 🔴 <b>Review finding 3b</b>: the first version derived BOTH sides with the same reflection
    /// expression the production code uses, so it was circular and could not expose a reflection
    /// change. The expected set is now an explicit literal — a hand list is exactly right HERE,
    /// because a test's oracle must be INDEPENDENT of the thing it checks.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_envelope_field_is_covered_by_the_comparison()
    {
        var (file, wire) = ConstructedPair();
        var compared = FrameFieldParity.Compare(file, wire)
            .Select(c => c.Field)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "Body", "Origin", "SenderActor", "SenderNode", "Sequence", "Signal" },
            compared);
    }

    /// <summary>
    /// FR-006 — a ruled divergence PASSES, is REPORTED with both values and its ruling, and now
    /// also carries its SOURCE PROVENANCE (🔴 review finding 4: FR-008 required provenance as data;
    /// it existed only in comments the report could never print).
    /// </summary>
    [Fact]
    public void Ruled_divergences_are_reported_with_both_values_their_ruling_and_their_source()
    {
        var (file, wire) = ConstructedPair();
        var comparisons = FrameFieldParity.Compare(file, wire);

        var report = FrameFieldParity.Report(comparisons);
        _out.WriteLine(report);

        var ruled = comparisons.Where(c => c.Verdict == FrameFieldVerdict.DivergesRuled).ToList();
        Assert.NotEmpty(ruled);

        foreach (var c in ruled)
        {
            Assert.NotNull(c.Ruling);
            Assert.NotEmpty(c.Ruling!.RulingId);
            Assert.NotEmpty(c.Ruling.Rationale);
            Assert.NotEmpty(c.Ruling.FilePlaneSource);
            Assert.NotEmpty(c.Ruling.WirePlaneSource);
            Assert.NotEqual(c.FilePlaneValue, c.WirePlaneValue);

            Assert.Contains(c.Ruling.RulingId, report, StringComparison.Ordinal);
            Assert.Contains(c.FilePlaneValue, report, StringComparison.Ordinal);
            Assert.Contains(c.WirePlaneValue, report, StringComparison.Ordinal);
            Assert.Contains(c.Ruling.FilePlaneSource, report, StringComparison.Ordinal);
            Assert.Contains(c.Ruling.WirePlaneSource, report, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// SC-002 / SC-005 — the divergence COUNT is produced by the check, never typed by a person.
    /// Three fields are ruled may-diverge; Sequence agrees after the Q-110-02 standardisation.
    /// </summary>
    [Fact]
    public void Exactly_the_three_ruled_fields_diverge_and_sequence_agrees()
    {
        var (file, wire) = ConstructedPair();
        var comparisons = FrameFieldParity.Compare(file, wire);
        _out.WriteLine(FrameFieldParity.Report(comparisons));

        var diverging = comparisons
            .Where(c => c.Verdict != FrameFieldVerdict.Agrees)
            .Select(c => c.Field)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "Origin", "SenderActor", "SenderNode" }, diverging);

        var sequence = comparisons.Single(c => c.Field == "Sequence");
        Assert.Equal(FrameFieldVerdict.Agrees, sequence.Verdict);
        Assert.Equal("1", sequence.FilePlaneValue);
        Assert.Equal("1", sequence.WirePlaneValue);
    }

    /// <summary>
    /// 🔴 FR-010 / SC-003 — <b>THE NEGATIVE CONTROL, REWRITTEN AFTER REVIEW FINDING 2.</b>
    ///
    /// <para>
    /// The first version was <b>not</b> a real control and the reviewer was right to kill it. It
    /// took a correctly-constructed pair and mutated one with <c>wire with { Body = ... }</c> — so
    /// it tampered with a RECORD, never with a CARRIER, and it asserted a classifier verdict
    /// instead of driving the acceptance gate. Worse, the "observed failing" evidence recorded for
    /// it was misattributed: that test went red pre-fix because <c>Sequence</c> was still divergent,
    /// NOT because the Body tamper was detected. <b>An evidence file that credits the wrong cause is
    /// worse than no evidence, because it retires the question.</b>
    /// </para>
    ///
    /// <para>
    /// This version makes the two CARRIERS genuinely disagree — each is asked to carry a different
    /// body through its own real <c>BuildFrame</c> path — and then asserts that
    /// <see cref="FrameFieldParity.Accepts"/>, <b>the same gate the passing test calls</b>, goes
    /// false and names the field.
    /// </para>
    /// </summary>
    [Fact]
    public void The_gate_rejects_when_the_two_carriers_genuinely_disagree_on_a_must_agree_field()
    {
        // Divergence produced BY THE CARRIERS: each constructs its own frame, with different bodies.
        var (file, wire) = ConstructedPair(fileBody: Body, wireBody: Body + "-DIVERGED");

        var comparisons = FrameFieldParity.Compare(file, wire);
        _out.WriteLine(FrameFieldParity.Report(comparisons));

        // The REAL gate must reject. This is the assertion the passing test relies on.
        Assert.False(FrameFieldParity.Accepts(comparisons));

        var body = comparisons.Single(c => c.Field == "Body");
        Assert.Equal(FrameFieldVerdict.DivergesUnruled, body.Verdict);
        Assert.Contains("DIVERGES-UNRULED", body.ToString(), StringComparison.Ordinal);

        // Control on the control: ONLY Body is unruled-divergent, so the rejection is attributable
        // to the divergence introduced and not to some unrelated field (the exact misattribution
        // that made the previous evidence worthless).
        Assert.Equal(
            new[] { "Body" },
            comparisons.Where(c => c.Verdict == FrameFieldVerdict.DivergesUnruled)
                       .Select(c => c.Field).ToArray());
    }

    /// <summary>
    /// 🔴 SC-004, <b>REWRITTEN AFTER REVIEW FINDING 3.</b>
    ///
    /// <para>
    /// The first version asserted "there are currently no unruled fields" — which proves the
    /// OPPOSITE of its own name and would pass even if <c>Compare</c> silently treated every
    /// unknown differing field as agreeing. With the production table every field is ruled, so the
    /// property is undemonstrable through it. This drives the ruling-table overload with
    /// <c>SenderActor</c> REMOVED and requires the now-unruled, genuinely divergent field to come
    /// back <see cref="FrameFieldVerdict.DivergesUnruled"/> and to fail the gate.
    /// </para>
    /// </summary>
    [Fact]
    public void A_differing_field_with_no_ruling_is_unruled_and_fails_the_gate()
    {
        var (file, wire) = ConstructedPair();

        // SenderActor genuinely differs between the carriers. Remove its ruling and it must stop
        // being permitted — the field is untouched, only the ruling is.
        var withoutSenderActor = FrameFieldParity.Rulings
            .Where(r => r.Field != "SenderActor")
            .ToList();

        var comparisons = FrameFieldParity.Compare(file, wire, withoutSenderActor);
        _out.WriteLine(FrameFieldParity.Report(comparisons));

        var senderActor = comparisons.Single(c => c.Field == "SenderActor");
        Assert.Equal(FrameFieldVerdict.DivergesUnruled, senderActor.Verdict);
        Assert.Null(senderActor.Ruling);
        Assert.False(FrameFieldParity.Accepts(comparisons));

        // And with the ruling restored the very same pair is accepted — proving the verdict tracks
        // the RULING and not some incidental property of the frames.
        Assert.True(FrameFieldParity.Accepts(FrameFieldParity.Compare(file, wire)));
    }

    /// <summary>FR-013 — numbering starts at 1 and increments.</summary>
    [Fact]
    public void Frame_sequence_numbering_starts_at_one_and_increments()
    {
        var file = new CoopFileOutbound(Self, Peer, _root);

        Assert.Equal(1, file.BuildFrame(Signal, Body).Sequence);
        Assert.Equal(2, file.BuildFrame(Signal, Body).Sequence);
        Assert.Equal(3, file.BuildFrame(Signal, Body).Sequence);
    }

    /// <summary>
    /// 🔴 <b>Review finding 6</b> — the reachability check still precedes the increment, but
    /// nothing pinned it, so moving the increment above the check would have passed the whole
    /// suite. A refused send MUST NOT consume a sequence number: if it did, an unreachable peer
    /// would silently punch holes in the numbering of a stream a reader may treat as gapless.
    /// </summary>
    [Fact]
    public void A_send_refused_for_an_unreachable_peer_does_not_consume_a_sequence_number()
    {
        var outbound = new CoopFileOutbound(Self, Peer, _root);

        Assert.False(outbound.PeerIsReachable);
        Assert.False(outbound.Send("M6_LOST", "x"));
        Assert.False(outbound.Send("M6_LOST", "x"));

        // Now make the peer reachable; the first frame that actually goes out must still be #1.
        Directory.CreateDirectory(CoopLayout.InboxOf(_root, Peer));
        Assert.True(outbound.Send("M6_FOUND", "y"));

        var written = Directory.EnumerateFiles(CoopLayout.InboxOf(_root, Peer)).Single();
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(written));
        Assert.Equal(1, doc.RootElement.GetProperty("Sequence").GetInt64());
    }

    /// <summary>
    /// FR-013 — frame filenames stay unique after the basis change. Uniqueness is the GUID's job,
    /// not the sequence's; assert it rather than assume it.
    /// </summary>
    [Fact]
    public void Frame_filenames_stay_unique_across_a_burst_of_sends()
    {
        var peerInbox = CoopLayout.InboxOf(_root, Peer);
        Directory.CreateDirectory(peerInbox);

        var outbound = new CoopFileOutbound(Self, Peer, _root);
        for (var i = 0; i < 50; i++)
            Assert.True(outbound.Send("M6_BURST", "payload-" + i));

        var written = Directory.EnumerateFiles(peerInbox).Select(Path.GetFileName).ToList();

        Assert.Equal(50, written.Count);
        Assert.Equal(50, written.Distinct(StringComparer.Ordinal).Count());
    }
}
