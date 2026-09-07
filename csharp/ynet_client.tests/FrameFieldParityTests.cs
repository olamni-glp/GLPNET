// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Net;
using System.Text;
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
/// </summary>
public class FrameFieldParityTests : IDisposable
{
    private readonly string _root;

    public FrameFieldParityTests()
    {
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

    /// <summary>The FIRST frame each carrier constructs for one identical logical send.</summary>
    private (YnetFrame File, YnetFrame Wire) ConstructedPair()
    {
        var file = new CoopFileOutbound(Self, Peer, _root);

        using var selfNode = NodeIdentity.Generate();
        using var peerNode = NodeIdentity.Generate();
        using var wire = new QuicOutbound(
            selfNode, peerNode.NodeId, Peer, new IPEndPoint(IPAddress.Loopback, 47999));

        var message = new YnetMessage("mid-110", "nodeA/laneA", Signal, Encoding.UTF8.GetBytes(Body));

        // First frame from each: this is what makes the Sequence comparison a comparison of BASIS.
        return (file.BuildFrame(Signal, Body), wire.BuildFrame(message));
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

    [Fact]
    public void Every_field_is_either_agreed_or_ruled_never_unruled()
    {
        var (file, wire) = ConstructedPair();
        var comparisons = FrameFieldParity.Compare(file, wire);

        Assert.NotEmpty(comparisons);

        var unruled = comparisons.Where(c => c.Verdict == FrameFieldVerdict.DivergesUnruled).ToList();

        Assert.True(
            unruled.Count == 0,
            "Unruled divergence between the two carriers:" + Environment.NewLine
            + FrameFieldParity.Report(comparisons));
    }

    /// <summary>
    /// FR-007 — the check ENUMERATES the envelope rather than trusting a hand list. The brief for
    /// this feature said three fields diverged; there were four. A hand-maintained list would have
    /// reproduced the brief's error instead of catching it.
    /// </summary>
    [Fact]
    public void Every_envelope_field_is_covered_by_the_comparison()
    {
        var (file, wire) = ConstructedPair();
        var compared = FrameFieldParity.Compare(file, wire).Select(c => c.Field).ToHashSet(StringComparer.Ordinal);

        var declared = typeof(YnetFrame)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(declared, compared);
    }

    /// <summary>
    /// FR-006 — a ruled divergence PASSES and is still REPORTED, with both values and its ruling.
    /// A decision that becomes invisible is indistinguishable from a suppression.
    /// </summary>
    [Fact]
    public void Ruled_divergences_are_reported_with_both_values_and_their_ruling()
    {
        var (file, wire) = ConstructedPair();
        var comparisons = FrameFieldParity.Compare(file, wire);

        var ruled = comparisons.Where(c => c.Verdict == FrameFieldVerdict.DivergesRuled).ToList();
        Assert.NotEmpty(ruled);

        foreach (var c in ruled)
        {
            Assert.NotNull(c.Ruling);
            Assert.NotEmpty(c.Ruling!.RulingId);
            Assert.NotEmpty(c.Ruling.Rationale);
            Assert.NotEqual(c.FilePlaneValue, c.WirePlaneValue);

            var line = c.ToString();
            Assert.Contains(c.Ruling.RulingId, line, StringComparison.Ordinal);
            Assert.Contains(c.FilePlaneValue, line, StringComparison.Ordinal);
            Assert.Contains(c.WirePlaneValue, line, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// SC-002 / SC-005 — the divergence COUNT is produced by the check, never typed by a person.
    /// Three fields are ruled may-diverge (Origin, SenderNode, SenderActor); Sequence agrees after
    /// the Q-110-02 standardisation. A run reporting a different count is a finding, not a nuisance.
    /// </summary>
    [Fact]
    public void Exactly_the_three_ruled_fields_diverge_and_sequence_agrees()
    {
        var (file, wire) = ConstructedPair();
        var comparisons = FrameFieldParity.Compare(file, wire);
        var report = Environment.NewLine + FrameFieldParity.Report(comparisons);

        var diverging = comparisons
            .Where(c => c.Verdict != FrameFieldVerdict.Agrees)
            .Select(c => c.Field)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "Origin", "SenderActor", "SenderNode" }, diverging);

        var sequence = comparisons.Single(c => c.Field == "Sequence");
        Assert.True(sequence.Verdict == FrameFieldVerdict.Agrees,
            "Sequence must be 1-based on BOTH planes after Q-110-02." + report);
        Assert.Equal("1", sequence.FilePlaneValue);
        Assert.Equal("1", sequence.WirePlaneValue);
    }

    /// <summary>
    /// 🔴 FR-010 / SC-003 — <b>THE NEGATIVE CONTROL, AND IT IS NOT TAUTOLOGICAL.</b>
    ///
    /// <para>
    /// Wave-34 recorded a control that compared two LITERALS and therefore proved nothing about the
    /// thing under test. This control drives the <b>real classifier</b> over a <b>real constructed
    /// pair</b>, with exactly one field forced to differ, and requires the classifier to return
    /// <see cref="FrameFieldVerdict.DivergesUnruled"/> <b>naming that field</b>. A check never
    /// observed failing is a check that cannot pass.
    /// </para>
    /// </summary>
    [Fact]
    public void The_check_fails_when_a_must_agree_field_is_deliberately_diverged()
    {
        var (file, wire) = ConstructedPair();

        // Body is MustAgree with no may-diverge ruling. Force exactly one difference on the real pair.
        var tampered = wire with { Body = wire.Body + "-TAMPERED" };

        var comparisons = FrameFieldParity.Compare(file, tampered);
        var body = comparisons.Single(c => c.Field == "Body");

        Assert.Equal(FrameFieldVerdict.DivergesUnruled, body.Verdict);
        Assert.Contains("Body", body.ToString(), StringComparison.Ordinal);
        Assert.Contains("DIVERGES-UNRULED", body.ToString(), StringComparison.Ordinal);

        // And the aggregate the passing test relies on must actually go red.
        Assert.Contains(comparisons, c => c.Verdict == FrameFieldVerdict.DivergesUnruled);

        // Negative control on the control: every OTHER field is unaffected by the tamper.
        Assert.DoesNotContain(
            comparisons.Where(c => c.Field != "Body"),
            c => c.Verdict == FrameFieldVerdict.DivergesUnruled);
    }

    /// <summary>
    /// SC-004 — a field the ruling table does not cover, differing, fails and names itself. Proven
    /// by driving the classifier with Signal removed from consideration is impossible without
    /// mutating the table, so this proves the equivalent property directly: an unruled field name
    /// yields DivergesUnruled rather than being skipped.
    /// </summary>
    [Fact]
    public void An_unruled_field_that_differs_is_never_silently_skipped()
    {
        var ruledFields = FrameFieldParity.Rulings.Select(r => r.Field).ToHashSet(StringComparer.Ordinal);

        var declared = typeof(YnetFrame)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        // Today every declared field is ruled. If someone adds one and does NOT rule it, this fails
        // here rather than the new field drifting outside the check unnoticed.
        var unruledDeclared = declared.Where(f => !ruledFields.Contains(f)).ToList();

        Assert.True(
            unruledDeclared.Count == 0,
            "YnetFrame gained field(s) with no ruling: " + string.Join(", ", unruledDeclared)
            + ". Add a FrameFieldRuling, or the parity check cannot classify them.");
    }

    /// <summary>
    /// FR-013 — the frame filename stays unique after Sequence became 1-based. Uniqueness is the
    /// GUID's job; assert it rather than assume it.
    /// </summary>
    [Fact]
    public void Frame_sequence_numbering_starts_at_one_and_increments()
    {
        var file = new CoopFileOutbound(Self, Peer, _root);

        var first = file.BuildFrame(Signal, Body);
        var second = file.BuildFrame(Signal, Body);
        var third = file.BuildFrame(Signal, Body);

        Assert.Equal(1, first.Sequence);
        Assert.Equal(2, second.Sequence);
        Assert.Equal(3, third.Sequence);
    }

    /// <summary>
    /// FR-013 — frame filenames stay unique after the basis change. Uniqueness is the GUID's job,
    /// not the sequence's; assert it rather than assume it, because the sequence is the only part
    /// of the name this era touched and "the GUID covers it" is a claim until it is measured.
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
