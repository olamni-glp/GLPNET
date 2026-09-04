// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The federated board operation (feature 102, T006).
//
// Contract federation-wire.md W3 / data-model I-13..I-16 / FR-009, FR-010, FR-011, FR-017.
//
// Reuses Crdt.Dot (op identity) and Crdt.HashChain (pred-hash) UNCHANGED — this type is an
// envelope around primitives that already exist and are already tested, not a new identity scheme.
//
// NOTE ON WHAT IS ABSENT. There is no Delete, no Withdraw, no Remove, no Tombstone here, and none
// may be added (FR-017 / I-16). The correction mechanism is RetirementOp — an APPENDED superseding
// op. On an append-only board a removal is indistinguishable from a suppression, so the capability
// is absent by construction rather than guarded against at call sites.

using System.Text.Json;
using System.Text.Json.Serialization;
using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>
/// A board operation as it crosses the link and as it rests in a per-actor log.
/// </summary>
public sealed record FederationOp
{
    /// <summary>The exactly-once fold key (FR-010). Reuses the existing DVV dot.</summary>
    public required Dot OpId { get; init; }

    /// <summary>The originating participant's node id. MUST survive the crossing (FR-009).</summary>
    public required string Origin { get; init; }

    /// <summary>Operation kind — opaque to federation except for <see cref="RetirementOp.Kind"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Present iff the operation is leadership-bearing. ABSENT IS NOT TERM ZERO: an op with no term
    /// is never a candidate in an ordering decision at all.
    /// </summary>
    public Term? Term { get; init; }

    /// <summary>Causal context (existing DVV semantics).</summary>
    public IReadOnlyList<Dot> Deps { get; init; } = Array.Empty<Dot>();

    /// <summary>Day-one hash chain over (deps, self) — existing <see cref="HashChain"/>.</summary>
    public byte[] PredHash { get; init; } = Array.Empty<byte>();

    /// <summary>Opaque body. Federation neither reads nor validates it.</summary>
    public JsonElement Body { get; init; }

    /// <summary>
    /// Base64 signature by the ORIGIN's federation identity over <see cref="ToSignableJson"/>.
    /// <para>
    /// Absent is a real state, not an error: a host whose peers have not published their public keys
    /// cannot verify anything, and that is reported as <c>UnverifiedOrigin</c> rather than silently
    /// treated as either valid or forged (see <see cref="OpAttribution"/>). What is NOT permitted is
    /// folding a signed-key-known origin's op with no signature.
    /// </para>
    /// </summary>
    public string? Signature { get; init; }

    /// <summary>Build an op, computing its pred-hash from (id, deps) exactly as <see cref="Op.Create"/> does.</summary>
    public static FederationOp Create(Dot opId, string origin, string kind, JsonElement body,
                                      Term? term = null, IReadOnlyList<Dot>? deps = null)
    {
        // THE INVARIANT BELONGS AT CONSTRUCTION, not only at the decoder.
        //
        // Enforcing it in FromJson alone left three ways in: this factory, the scheduler-native
        // adapter that calls it, and any local caller. A nonpositive counter is reported as
        // ALREADY HELD by every frontier (whose contiguous run starts at 0), so an operation
        // carrying one can never be recovered after a lost push.
        if (opId.Counter < 1)
            throw new ArgumentOutOfRangeException(nameof(opId),
                $"dot counter must be >= 1; got {opId.Counter}. Every frontier reports a nonpositive "
                + "counter as already-held, so such an operation could never be reconciled.");

        var d = deps ?? Array.Empty<Dot>();
        return new FederationOp
        {
            OpId = opId,
            Origin = origin,
            Kind = kind,
            Term = term,
            Deps = d,
            PredHash = HashChain.PredHash(opId, d),
            Body = body,
        };
    }

    /// <summary>
    /// Canonical JSON per contract W3. Deterministic property order and no whitespace, so two hosts
    /// serialising the same op produce BYTE-IDENTICAL output — which is what makes the SC-003
    /// byte-equality assertion meaningful rather than a comparer's opinion.
    /// </summary>
    public string ToCanonicalJson()
    {
        string body = ToSignableJson();
        if (Signature is null) return body;
        // The signature is appended OUTSIDE the signed region — see ToSignableJson.
        return body[..^1] + ",\"sig\":" + JsonSerializer.Serialize(Signature) + "}";
    }

    /// <summary>
    /// The canonical form WITHOUT the signature — the exact bytes an origin signature covers.
    /// <para>
    /// Signing must cover the bytes that cross the wire, minus the signature itself. Deriving the
    /// signed region from a different serialisation is how signature checks come to pass over bytes
    /// nobody ever transmitted.
    /// </para>
    /// </summary>
    public string ToSignableJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"op_id\":{\"peer\":").Append(JsonSerializer.Serialize(OpId.PeerName))
          .Append(",\"counter\":").Append(OpId.Counter).Append('}');
        sb.Append(",\"origin\":").Append(JsonSerializer.Serialize(Origin));
        sb.Append(",\"kind\":").Append(JsonSerializer.Serialize(Kind));
        if (Term is { } t)
        {
            sb.Append(",\"term\":{\"space\":").Append(JsonSerializer.Serialize(t.SpaceId))
              .Append(",\"era_counter\":").Append(t.EraCounter)
              .Append(",\"host\":").Append(JsonSerializer.Serialize(t.HostId)).Append('}');
        }
        sb.Append(",\"deps\":[");
        for (int i = 0; i < Deps.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"peer\":").Append(JsonSerializer.Serialize(Deps[i].PeerName))
              .Append(",\"counter\":").Append(Deps[i].Counter).Append('}');
        }
        sb.Append(']');
        sb.Append(",\"pred_hash\":").Append(JsonSerializer.Serialize(Convert.ToHexStringLower(PredHash)));
        sb.Append(",\"body\":").Append(Body.ValueKind == JsonValueKind.Undefined ? "null" : Body.GetRawText());
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>Parse the canonical form. A malformed op is a loud fault, never a silently-skipped line.</summary>
    public static FederationOp FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;

        var idEl = r.GetProperty("op_id");
        long counter = idEl.GetProperty("counter").GetInt64();

        // COUNTERS START AT 1. FederationFrontier's contiguous run defaults to 0, so Contains()
        // reports every nonpositive dot as ALREADY HELD — and if such an operation's push were
        // lost, the reconciliation pull would suppress it permanently and the replicas diverge in
        // silence. Refusing it at the decoder is the only place that catches it before the frontier
        // has already told the lie.
        if (counter < 1)
            throw new FormatException(
                $"op_id.counter must be >= 1; got {counter}. A nonpositive counter is reported as "
                + "already-held by every frontier, so a lost operation could never be recovered.");

        var opId = new Dot(idEl.GetProperty("peer").GetString()!, counter);

        Term? term = null;
        if (r.TryGetProperty("term", out var tEl) && tEl.ValueKind == JsonValueKind.Object)
        {
            term = new Term(
                tEl.TryGetProperty("space", out var sp) && sp.ValueKind == JsonValueKind.String
                    ? sp.GetString()! : TermSpace.LegacyId,
                tEl.GetProperty("era_counter").GetInt64(),
                tEl.TryGetProperty("host", out var h) && h.ValueKind == JsonValueKind.String
                    ? h.GetString()! : string.Empty);
        }

        var deps = new List<Dot>();
        if (r.TryGetProperty("deps", out var dEl) && dEl.ValueKind == JsonValueKind.Array)
            foreach (var d in dEl.EnumerateArray())
                deps.Add(new Dot(d.GetProperty("peer").GetString()!, d.GetProperty("counter").GetInt64()));

        byte[] pred = r.TryGetProperty("pred_hash", out var pEl) && pEl.ValueKind == JsonValueKind.String
            ? Convert.FromHexString(pEl.GetString()!)
            : Array.Empty<byte>();

        // THE HASH IS CHECKED, NOT JUST CARRIED. Accepting any supplied bytes let malformed causal
        // chain data into the durable fold in the default unverified-origin mode — the pred-hash is
        // the day-one integrity chain over (opId, deps), so a value that does not recompute is not a
        // weaker claim, it is a false one.
        var expected = HashChain.PredHash(opId, deps);
        if (pred.Length != 0 && !pred.AsSpan().SequenceEqual(expected))
            throw new FormatException(
                "pred_hash does not match HashChain.PredHash(op_id, deps) — the causal chain this "
                + "operation asserts is not the one its own fields produce.");

        return new FederationOp
        {
            OpId = opId,
            Origin = r.GetProperty("origin").GetString()!,
            Kind = r.GetProperty("kind").GetString()!,
            Term = term,
            Deps = deps,
            PredHash = pred,
            Body = r.TryGetProperty("body", out var b) ? b.Clone() : default,
            Signature = r.TryGetProperty("sig", out var sg) && sg.ValueKind == JsonValueKind.String
                ? sg.GetString() : null,
        };
    }

    /// <summary>
    /// A copy carrying an origin signature. Signing is a separate step from construction because the
    /// signable bytes are only fixed once every other field is.
    /// </summary>
    public FederationOp SignedBy(System.Security.Cryptography.X509Certificates.X509Certificate2 identity) =>
        this with { Signature = OpAttribution.Sign(this with { Signature = null }, identity) };
}

/// <summary>
/// An operation's ordering disposition on this host. THREE values (FR-031 / SC-015) plus the
/// no-term case — collapsing any two of these is the defect SC-015 is written to catch.
/// </summary>
public enum OrderingDisposition
{
    /// <summary>Leadership-bearing, in the live epoch: participates in ordering.</summary>
    Orderable,

    /// <summary>Leadership-bearing but in the legacy space: retained, never wins (FR-027).</summary>
    UnorderedLegacy,

    /// <summary>Leadership-bearing in a space this host does not recognise: retained, reported unordered (FR-016).</summary>
    UnorderedUnknownSpace,

    /// <summary>Carries no term at all: not a candidate in any ordering decision. Absent is not zero.</summary>
    NotLeadershipBearing,
}
