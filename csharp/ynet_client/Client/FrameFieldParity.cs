// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT

using System.Reflection;

namespace Ynet.Client;

/// <summary>Whether the two planes are required to agree on a field.</summary>
public enum FrameFieldOutcome
{
    /// <summary>The planes MUST produce the same value. A difference is a defect.</summary>
    MustAgree,

    /// <summary>The planes MAY differ, by an engineer ruling that states why.</summary>
    MayDiverge,
}

/// <summary>
/// One engineer ruling about one envelope field.
///
/// <para>
/// 🔴 These are DATA, not comments (feature 110, FR-014). A comment saying "this divergence is
/// intentional" cannot be revoked without editing code, and cannot be read by a check. Holding the
/// ruling as data means withdrawing it re-fires the check with no code change — which is the whole
/// difference between a decision that stays honest and one that quietly becomes a suppression.
/// </para>
/// </summary>
/// <param name="Field">Envelope property name.</param>
/// <param name="Outcome">Whether the planes must agree.</param>
/// <param name="RulingId">The ruling that authorises this, e.g. <c>Q-110-01</c>. Empty when the
/// field simply carries the caller's input verbatim and needs no ruling to agree.</param>
/// <param name="Rationale">Why the divergence is legitimate. Required for
/// <see cref="FrameFieldOutcome.MayDiverge"/>.</param>
/// <param name="FilePlaneSource">Where the file carrier populates this field, as file:line.
/// FR-008: the provenance is DATA the report can print, not prose in a comment that no reader of
/// the output ever sees.</param>
/// <param name="WirePlaneSource">Where the wire carrier populates this field, as file:line.</param>
public sealed record FrameFieldRuling(
    string Field,
    FrameFieldOutcome Outcome,
    string RulingId,
    string Rationale,
    string FilePlaneSource,
    string WirePlaneSource);

/// <summary>The verdict for one field of one constructed-frame pair.</summary>
public enum FrameFieldVerdict
{
    /// <summary>Both planes produced the same value.</summary>
    Agrees,

    /// <summary>The planes differ, and a ruling permits it. Passes — and is still REPORTED.</summary>
    DivergesRuled,

    /// <summary>The planes differ with no ruling permitting it. This is the failure case.</summary>
    DivergesUnruled,
}

/// <summary>One field's comparison, carrying BOTH values so a reader never has to open the source.</summary>
public sealed record FrameFieldComparison(
    string Field,
    FrameFieldVerdict Verdict,
    string FilePlaneValue,
    string WirePlaneValue,
    FrameFieldRuling? Ruling)
{
    /// <summary>
    /// A line a human can act on. Ruled divergences print their ruling id and rationale, so an
    /// intentional divergence is VISIBLE in every run rather than absent from it (FR-006) — the
    /// distinction between a declared decision and a silently suppressed one.
    /// </summary>
    public override string ToString() => Verdict switch
    {
        FrameFieldVerdict.Agrees =>
            $"{Field}: AGREES ('{FilePlaneValue}')",
        FrameFieldVerdict.DivergesRuled =>
            $"{Field}: DIVERGES-RULED [{Ruling!.RulingId}] file='{FilePlaneValue}' wire='{WirePlaneValue}' — {Ruling.Rationale}",
        _ =>
            $"{Field}: DIVERGES-UNRULED file='{FilePlaneValue}' wire='{WirePlaneValue}' — no ruling permits this",
    };
}

/// <summary>
/// FR-010 / SC-003 — <b>one protocol, two planes</b>, measured at CONSTRUCTION.
///
/// <para>
/// The file carrier and the QUIC carrier share the <see cref="YnetFrame"/> envelope type and
/// encode it identically. They <b>construct</b> it differently. Measured at source on
/// <c>develop</c>, 2026-09-07T18:05Z, four fields diverged:
/// <c>Origin</c>, <c>Sequence</c>, <c>SenderNode</c>, <c>SenderActor</c>.
/// </para>
///
/// <para>
/// 🔴 <b>The roadmap brief said three. There were four.</b> <c>SenderNode</c> was missing from it.
/// That is exactly why <see cref="Compare"/> ENUMERATES the envelope by reflection instead of
/// walking a hand-maintained list: a hand list is a claim that goes stale silently, and this
/// feature exists because one already had.
/// </para>
///
/// <para>
/// This type deliberately does NOT decide which plane is right. Field MEANING is a fleet protocol
/// decision, not a test decision (C-18/C-19). It reports; rulings decide.
/// </para>
/// </summary>
public static class FrameFieldParity
{
    /// <summary>
    /// The rulings in force. Q-110-01/02/03, engineer, 2026-09-07.
    ///
    /// <para>
    /// <c>Signal</c> and <c>Body</c> carry the caller's input verbatim on both planes, so they
    /// need no ruling to be expected to agree — they are <see cref="FrameFieldOutcome.MustAgree"/>
    /// with an empty ruling id, which is a different thing from an unruled divergence.
    /// </para>
    /// </summary>
    public static IReadOnlyList<FrameFieldRuling> Rulings { get; } =
    [
        new("Origin", FrameFieldOutcome.MayDiverge, "Q-110-03",
            "the wire has a handshake-proven Ed25519 NodeId the file plane cannot have; "
            + "the file plane uses node/actor identity",
            "CoopFileCarrier.cs BuildFrame: _self.Identity",
            "QuicCarrier.cs BuildFrame: _self.NodeId.ToString()"),

        new("SenderNode", FrameFieldOutcome.MayDiverge, "Q-110-03",
            "as Origin: Ed25519 NodeId on the wire, node name on the file plane",
            "CoopFileCarrier.cs BuildFrame: _self.Node",
            "QuicCarrier.cs BuildFrame: _self.NodeId.ToString()"),

        new("SenderActor", FrameFieldOutcome.MayDiverge, "Q-110-01",
            "the two planes have different addressing models: the file plane carries the SENDER's "
            + "actor, the wire carries the DESTINATION's actor. Ruled legitimate, and the residual "
            + "risk is named not closed: a field whose meaning depends on the carrier is what makes "
            + "a cross-plane defect hard to reproduce, which is why this comparison reports it every run",
            "CoopFileCarrier.cs BuildFrame: _self.Actor (the SENDER)",
            "QuicCarrier.cs BuildFrame: _peer.Actor (the DESTINATION)"),

        new("Sequence", FrameFieldOutcome.MustAgree, "Q-110-02",
            "standardised 1-based on both planes; the first message is #1",
            "CoopFileCarrier.cs BuildFrame: Interlocked.Increment(ref _sequence)",
            "QuicCarrier.cs BuildFrame: Interlocked.Increment(ref _sequence)"),

        new("Signal", FrameFieldOutcome.MustAgree, "", "carries the caller's summary verbatim",
            "CoopFileCarrier.cs BuildFrame: signal",
            "QuicCarrier.cs BuildFrame: message.Summary"),

        new("Body", FrameFieldOutcome.MustAgree, "", "carries the caller's body verbatim",
            "CoopFileCarrier.cs BuildFrame: body",
            "QuicCarrier.cs BuildFrame: Encoding.UTF8.GetString(message.Body.Span)"),
    ];

    /// <summary>
    /// Compare the frame each plane constructs for one identical logical send.
    ///
    /// <para>
    /// 🔴 <b>Pass the FIRST frame from each carrier.</b> Sequence counters are per-carrier and
    /// independent, so absolute values can never be meaningfully equal across two long-running
    /// carriers. Comparing the FIRST frame of each makes the <c>Sequence</c> comparison a
    /// comparison of BASIS (0-based vs 1-based), which is the property that is actually ruled
    /// (FR-009). Handing this method the 5th file frame and the 2nd wire frame compares nothing.
    /// </para>
    ///
    /// <para>
    /// A field with no ruling that differs is <see cref="FrameFieldVerdict.DivergesUnruled"/> —
    /// never skipped. An envelope field added tomorrow and populated on one plane only therefore
    /// FAILS and names itself, rather than being quietly outside the check (FR-007).
    /// </para>
    /// </summary>
    public static IReadOnlyList<FrameFieldComparison> Compare(YnetFrame filePlane, YnetFrame wirePlane) =>
        Compare(filePlane, wirePlane, Rulings);

    /// <summary>
    /// As <see cref="Compare(YnetFrame, YnetFrame)"/>, but against a caller-supplied ruling table.
    ///
    /// <para>
    /// 🔴 This overload exists so SC-004 can be DEMONSTRATED rather than assumed. With the
    /// production table every envelope field happens to be ruled, so a test using it can only ever
    /// assert "there are currently no unruled fields" — which would pass even if this method
    /// silently treated an unknown differing field as agreeing. A test must be able to hand in a
    /// table with a field REMOVED and watch the field come back <see cref="FrameFieldVerdict.DivergesUnruled"/>.
    /// A reviewer caught the original test proving the opposite of its own name; this is the seam
    /// that fixes it.
    /// </para>
    /// </summary>
    public static IReadOnlyList<FrameFieldComparison> Compare(
        YnetFrame filePlane, YnetFrame wirePlane, IReadOnlyList<FrameFieldRuling> rulings)
    {
        ArgumentNullException.ThrowIfNull(filePlane);
        ArgumentNullException.ThrowIfNull(wirePlane);
        ArgumentNullException.ThrowIfNull(rulings);

        var byField = rulings.ToDictionary(r => r.Field, StringComparer.Ordinal);
        var results = new List<FrameFieldComparison>();

        foreach (var property in typeof(YnetFrame)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            var fileValue = Render(property.GetValue(filePlane));
            var wireValue = Render(property.GetValue(wirePlane));
            byField.TryGetValue(property.Name, out var ruling);

            var verdict = string.Equals(fileValue, wireValue, StringComparison.Ordinal)
                ? FrameFieldVerdict.Agrees
                : ruling is { Outcome: FrameFieldOutcome.MayDiverge }
                    ? FrameFieldVerdict.DivergesRuled
                    : FrameFieldVerdict.DivergesUnruled;

            results.Add(new FrameFieldComparison(property.Name, verdict, fileValue, wireValue, ruling));
        }

        return results;
    }

    /// <summary>Null renders as a distinct marker so "null" and the literal string "null" differ.</summary>
    private static string Render(object? value) =>
        value is null ? "<null>" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "<null>";

    /// <summary>
    /// THE ACCEPTANCE GATE, as one expression.
    ///
    /// <para>
    /// 🔴 Both the passing parity test and the negative control call THIS method. That is the
    /// point: a control that asserts something adjacent to the gate proves nothing about the gate.
    /// A reviewer found the first version of the control asserting a classifier verdict while the
    /// real acceptance assertion was written out longhand somewhere else — so the control could
    /// have passed with the gate broken.
    /// </para>
    /// </summary>
    public static bool Accepts(IReadOnlyList<FrameFieldComparison> comparisons)
    {
        ArgumentNullException.ThrowIfNull(comparisons);
        return comparisons.Count > 0
               && comparisons.All(c => c.Verdict != FrameFieldVerdict.DivergesUnruled);
    }

    /// <summary>A report a failing test can print whole — and a PASSING one should print too.
    /// Every field, never only the failures: a report that shows only what broke hides what is
    /// silently permitted, which is how a ruled divergence quietly becomes a suppression (FR-006).
    /// Ruled divergences carry their source provenance so a reader never opens a carrier.</summary>
    public static string Report(IReadOnlyList<FrameFieldComparison> comparisons)
    {
        ArgumentNullException.ThrowIfNull(comparisons);
        return string.Join(Environment.NewLine, comparisons.Select(c =>
            c.Verdict == FrameFieldVerdict.DivergesRuled
                ? $"  {c}{Environment.NewLine}      file <- {c.Ruling!.FilePlaneSource}{Environment.NewLine}      wire <- {c.Ruling.WirePlaneSource}"
                : "  " + c));
    }
}
