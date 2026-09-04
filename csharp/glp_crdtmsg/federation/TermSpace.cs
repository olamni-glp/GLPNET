// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Term-space classification (feature 102-quic-federation-transport, T004).
//
// Contract term-ordering.md C4 / data-model I-5..I-8 / FR-026, FR-027, FR-016, FR-031.
//
// WHY A SPACE EXISTS AT ALL. A live board carries a leader_claim op whose term is 5961694 =
// floor(unix_ts/300) — a wall-clock-derived term. Its emitting code was deleted; the OP STILL
// VOTES. Max-term is monotone, so a naive cross-host merge installs that fossil as the permanent
// winner and no later legitimate claim can ever outrank it. Ruling Q-GLPNETG27-03 placed a STOP
// ORDER on folding any board across hosts until the fold is space-aware. This file is the first
// half of lifting that order.
//
// THREE dispositions, never two (FR-031 / SC-015): Live, Legacy, and Unknown. Collapsing Unknown
// into Legacy would silently make an op from a term-space we have never heard of look like a
// deliberately-retired one — a different fact with a different remedy.

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>
/// How an operation's declared term-space relates to this host's configured live epoch.
/// Three values, deliberately: see FR-031 / SC-015.
/// </summary>
public enum SpaceKind
{
    /// <summary>The operation's space is this host's configured live federation epoch.</summary>
    Live,

    /// <summary>
    /// The operation declares no space at all. Pre-dates the term-space rule, or was deliberately
    /// retired into legacy by a <see cref="RetirementOp"/>. Retained, never dropped (FR-027).
    /// </summary>
    Legacy,

    /// <summary>
    /// The operation declares a space this host does not recognise. Retained and reported as
    /// UNORDERED — never coerced into the live space and never dropped (FR-016).
    /// </summary>
    Unknown,
}

/// <summary>
/// A named ordering universe. Operations in different spaces are incomparable by term (FR-014).
/// </summary>
public readonly record struct TermSpace(string Id, SpaceKind Kind)
{
    /// <summary>
    /// The reserved id for operations that declare no space. A NAME, not an absence — so that
    /// "retired into legacy" and "never had a space" are the same, inspectable, orderable-nowhere
    /// state rather than a null that every caller must remember to check.
    /// </summary>
    public const string LegacyId = "__legacy__";

    /// <summary>The legacy space singleton.</summary>
    public static TermSpace Legacy => new(LegacyId, SpaceKind.Legacy);

    public override string ToString() => $"{Id} [{Kind.ToString().ToLowerInvariant()}]";
}

/// <summary>
/// Classifies a declared space id against this host's configured live epoch, and mints new epochs.
/// </summary>
public sealed class TermSpaceRegistry
{
    private readonly string _liveEpochId;

    /// <param name="liveEpochId">
    /// The configured live epoch id. Minted by a recorded operator action (FR-026) — never derived
    /// from a host identity (that yields per-host spaces, in which no two terms are ever comparable
    /// and no leader can ever be elected) and never derived from wall-clock time (that is precisely
    /// how the fossil was born).
    /// </param>
    public TermSpaceRegistry(string liveEpochId)
    {
        if (string.IsNullOrWhiteSpace(liveEpochId))
            throw new ArgumentException("live epoch id must be non-empty; an unminted space cannot order anything (FR-026)", nameof(liveEpochId));
        if (liveEpochId == TermSpace.LegacyId)
            throw new ArgumentException($"'{TermSpace.LegacyId}' is reserved for the legacy space", nameof(liveEpochId));
        _liveEpochId = liveEpochId;
    }

    /// <summary>The configured live epoch.</summary>
    public TermSpace LiveEpoch => new(_liveEpochId, SpaceKind.Live);

    /// <summary>
    /// Classify a declared space id. A null/empty declaration is <see cref="SpaceKind.Legacy"/>
    /// (FR-027); an unrecognised non-empty declaration is <see cref="SpaceKind.Unknown"/> (FR-016).
    /// These are NOT the same result and MUST NOT be collapsed (FR-031 / SC-015).
    /// </summary>
    public TermSpace Classify(string? declaredSpaceId)
    {
        if (string.IsNullOrWhiteSpace(declaredSpaceId) || declaredSpaceId == TermSpace.LegacyId)
            return TermSpace.Legacy;
        if (declaredSpaceId == _liveEpochId)
            return LiveEpoch;
        return new TermSpace(declaredSpaceId, SpaceKind.Unknown);
    }

    /// <summary>
    /// True iff terms in this space participate in ordering decisions on this host. Only the live
    /// epoch does: legacy and unknown are retained and reported, but they never win.
    /// </summary>
    public bool IsOrderable(TermSpace space) => space.Kind == SpaceKind.Live;

    /// <summary>
    /// Reject an epoch id that looks clock-derived — all digits and long enough to be a unix-derived
    /// counter. This is the shape the fossil had; refusing it at mint time is cheaper than
    /// discovering it after a monotone merge (FR-015, contract G3).
    /// </summary>
    public static bool LooksClockDerived(string epochId)
    {
        if (string.IsNullOrWhiteSpace(epochId)) return false;

        // The fossil's shape: an all-digit counter.
        if (epochId.Length >= 6 && epochId.All(char.IsDigit)) return true;

        // AND THE SHAPE THIS CODE ITSELF EMITTED. The mint command produced
        // "ynet-epoch-2026-09-8240c4" and this guard passed it, because it only rejected all-digit
        // strings. A guard that cannot catch its own caller's output is decorative. FR-026 forbids
        // deriving the identifier from wall-clock time in ANY encoding, not just a unix counter.
        foreach (var part in epochId.Split('-', '_', '.', ':'))
        {
            // A plausible calendar year, on its own or leading a yyyyMM / yyyyMMdd run.
            if (part.Length is 4 && part.All(char.IsDigit)
                && int.TryParse(part, out var y) && y is >= 1970 and <= 2999)
                return true;

            if (part.Length is 6 or 8 && part.All(char.IsDigit)
                && int.TryParse(part[..4], out var y2) && y2 is >= 1970 and <= 2999)
                return true;
        }
        return false;
    }
}
