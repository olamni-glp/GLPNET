// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The merge gate (feature 102, T012).
//
// Contract term-ordering.md C7 / FR-018.
// Authority: engineer ruling Q-GLPNETG27-03 — the STOP ORDER on folding boards across hosts until
// BOTH sides are term-space aware.
//
// REFUSING IS THE CORRECT OUTCOME, not a degraded one. Merging under the older ordering rule is the
// irreversible mistake: term ordering is monotone, so a single merge that lets a wall-clock-derived
// term into the live space installs it as the permanent winner. There is no later fix — only a new
// epoch, which is a different board.

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>What a peer advertises about its own ordering rules.</summary>
public readonly record struct PeerCapabilities(bool TermSpaceAware, string? AdvertisedSpaceId);

/// <summary>Allow, or refuse with a SPECIFIC reason. "Merge failed" is not a reason.</summary>
public readonly record struct MergeVerdict(bool Allowed, string Reason)
{
    public static MergeVerdict Allow() => new(true, "both sides are term-space aware");
    public static MergeVerdict Refuse(string reason) => new(false, reason);
}

/// <summary>
/// A merge was refused by the gate. THROWN rather than returned as a count, because a silent no-op
/// would be indistinguishable from "the peer had nothing to send" — and the difference between
/// those two is the whole STOP ORDER.
/// </summary>
public sealed class MergeRefusedException : InvalidOperationException
{
    public MergeRefusedException(string reason) : base("merge refused: " + reason) { }
}

/// <summary>Decides whether a peer's board may be merged into this host's fold.</summary>
public static class MergeGate
{
    /// <summary>
    /// FR-018: refuse when EITHER side is not term-space aware.
    /// </summary>
    /// <param name="theirs">What the peer advertised during admission.</param>
    /// <param name="localTermSpaceAware">
    /// Whether this host can confirm its OWN awareness. Passed in rather than hard-coded true: a
    /// host that cannot confirm its own support must refuse, not assume. Assuming is how a gate
    /// stops being load-bearing.
    /// </param>
    public static MergeVerdict CanMerge(PeerCapabilities theirs, bool localTermSpaceAware)
    {
        if (!localTermSpaceAware)
            return MergeVerdict.Refuse("local term-space support unconfirmed — refusing rather than merging under the older ordering rule");

        if (!theirs.TermSpaceAware)
            return MergeVerdict.Refuse("peer is not term-space aware — merging would order its terms by magnitude alone (FR-018)");

        return MergeVerdict.Allow();
    }
}
