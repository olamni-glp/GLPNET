// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Retirement — the ONLY correction mechanism (feature 102, T011).
//
// Contract term-ordering.md C6 / data-model I-17..I-19 / FR-017, FR-029, SC-012.
// Authority: engineer ruling Q-GLPNETG28-04.
//
// WHY APPEND AND NOT DELETE. On an append-only board a removal is indistinguishable from a
// suppression: a reader cannot tell "this op was wrong and was corrected" from "someone did not
// want you to see this op". So the target is never removed. It stays in the log, visibly, and a
// SECOND op records that it has been assigned to the legacy space — where FR-014 makes it
// incomparable to every live term, which is the whole of the remedy.
//
// The live fossil this exists for: op 628016928ab854ae, term 5961694 = floor(unix_ts/300). Its
// emitting code was deleted; the op still votes. Deleting the emitter did not delete the op.
// DO NOT delete that op by any other means.

using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>
/// Builds and recognises retirement operations. A retirement is an ORDINARY board operation
/// (FR-029) — it folds, attributes and appends under exactly the same rules, and is itself
/// retirable.
/// </summary>
public static class RetirementOp
{
    /// <summary>The op kind that marks a retirement.</summary>
    public const string Kind = "retire";

    /// <summary>
    /// Build the superseding op that retires <paramref name="targetOpId"/> into the legacy space.
    /// </summary>
    /// <param name="opId">This retirement's own identity — it is a first-class op with its own dot.</param>
    /// <param name="origin">The node id retiring it. Attribution applies here as anywhere.</param>
    /// <param name="targetOpId">The operation being retired. It is NOT removed.</param>
    /// <param name="reason">Audit text. A retirement with no stated reason is not reviewable.</param>
    public static FederationOp Create(Dot opId, string origin, Dot targetOpId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("a retirement must state its reason — an unexplained retirement is not reviewable", nameof(reason));

        var body = JsonSerializer.SerializeToElement(new
        {
            target_op_id = new { peer = targetOpId.PeerName, counter = targetOpId.Counter },
            into_space = TermSpace.LegacyId,
            reason,
        });

        return FederationOp.Create(opId, origin, Kind, body);
    }

    /// <summary>True if this operation is a retirement.</summary>
    public static bool IsRetirement(FederationOp op) =>
        string.Equals(op.Kind, Kind, StringComparison.Ordinal);

    /// <summary>
    /// The operation a retirement targets, or null if this is not a well-formed retirement.
    /// Returning null rather than throwing: a malformed retirement must not be able to stop the
    /// fold, because the fold's job is to retain everything it is given.
    /// </summary>
    public static Dot? TargetOf(FederationOp op)
    {
        if (!IsRetirement(op) || op.Body.ValueKind != JsonValueKind.Object) return null;

        // THE WHOLE BODY MUST BE VALID, not just the target. Any operation merely NAMED "retire"
        // with a parsable target used to suppress that target from leadership — with no stated
        // reason and without declaring the legacy destination. Retirement is the only correction
        // mechanism on an append-only board (FR-017/FR-029); an incomplete one must not carry the
        // ordering consequence of a complete one.
        if (!op.Body.TryGetProperty("into_space", out var space)
            || space.ValueKind != JsonValueKind.String
            || !string.Equals(space.GetString(), TermSpace.LegacyId, StringComparison.Ordinal))
            return null;

        // FR-029: "a retirement with no stated reason is not reviewable." Create() enforces this on
        // the write side; the READ side has to enforce it too, or the wire is the way around it.
        if (!op.Body.TryGetProperty("reason", out var reason)
            || reason.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(reason.GetString()))
            return null;

        if (!op.Body.TryGetProperty("target_op_id", out var t) || t.ValueKind != JsonValueKind.Object) return null;
        if (!t.TryGetProperty("peer", out var p) || p.ValueKind != JsonValueKind.String) return null;
        if (!t.TryGetProperty("counter", out var c) || c.ValueKind != JsonValueKind.Number) return null;

        // TryGetInt64, not GetInt64. A JSON number can be fractional or outside Int64 — GetInt64
        // THROWS on both, contradicting this method's contract two lines above and, because the
        // caller once inserted the op first, leaving the fold partially mutated when the exception
        // escaped. A malformed retirement must not be able to stop the fold; that is the point.
        if (!c.TryGetInt64(out long counter)) return null;
        return new Dot(p.GetString()!, counter);
    }

    /// <summary>The stated reason, for the operator surface.</summary>
    public static string? ReasonOf(FederationOp op) =>
        IsRetirement(op) && op.Body.ValueKind == JsonValueKind.Object
            && op.Body.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String
            ? r.GetString() : null;
}
