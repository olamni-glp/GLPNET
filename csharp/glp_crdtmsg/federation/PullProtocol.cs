// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The reconciliation-pull wire protocol (feature 102, T034 completion).
//
// Contract federation-wire.md W5 / FR-028.
//
// ADDED BY ADVERSARIAL SELF-REVIEW. `ReconcileAsync` existed and was tested, but nothing ever
// SCHEDULED it and no frame ever carried a pull — while the console printed "pull every 60s" to the
// operator. A configured interval that no timer reads, and a method no frame invokes, is FR-028's
// pull leg existing on paper only. The console's claim was the tell: operator-facing output that
// states something the code does not do is the exact false-green class this era exists to remove.
//
// The exchange is FRONTIER FIRST, deliberately. Shipping the whole log every 60 s is a broadcast
// storm, not a backstop: with N hosts and M ops it is O(N·M) per interval forever, and it grows
// without bound precisely as the board becomes useful.

using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>
/// Wire-serialisable causal frontier: per peer, the contiguous run PLUS the counters seen above it.
/// <para>
/// The "above" half is what makes a hole repairable. An encoding that carried only a high-water mark
/// advertised counters this host had never received, and the peer then suppressed exactly the
/// operations the pull leg exists to recover.
/// </para>
/// </summary>
public static class FrontierCodec
{
    /// <summary>Canonical JSON, peers in ordinal order so two hosts produce identical bytes.</summary>
    public static string Encode(FederationFrontier f) => f.ToCanonicalJson();

    /// <summary>Parse a frontier. Tolerates the older bare-number encoding (see FederationFrontier).</summary>
    public static FederationFrontier Decode(string json) => FederationFrontier.FromCanonicalJson(json);
}

/// <summary>
/// The two pull frames. Separate boxes from <c>board</c> so a pull can never be mistaken for a
/// board append, and so a peer that does not implement pull simply never answers rather than
/// mis-folding a request as an operation.
/// </summary>
public static class PullProtocol
{
    /// <summary>Box for "here is my frontier, send me what I lack".</summary>
    public const string RequestBox = "pull-req";

    /// <summary>Box for "here are the ops you lack".</summary>
    public const string ResponseBox = "pull-resp";

    /// <summary>
    /// The largest response this protocol will build. Well under the transport's 64 MiB frame guard,
    /// which REJECTS an oversized frame — and a rejected frame is retried identically at every
    /// interval, so a peer more than one frame behind could never make partial progress. Batching is
    /// therefore a correctness requirement, not a tuning knob.
    /// </summary>
    public const int MaxResponseBytes = 8 * 1024 * 1024;

    /// <summary>Build a pull request carrying this host's frontier.</summary>
    public static byte[] EncodeRequest(FederationFrontier frontier) =>
        System.Text.Encoding.UTF8.GetBytes(FrontierCodec.Encode(frontier));

    /// <summary>Read the frontier out of a pull request.</summary>
    public static FederationFrontier DecodeRequest(byte[] bytes) =>
        FrontierCodec.Decode(System.Text.Encoding.UTF8.GetString(bytes));

    /// <summary>Build a pull response carrying only the ops the requester lacks.</summary>
    public static byte[] EncodeResponse(IReadOnlyList<FederationOp> ops) =>
        System.Text.Encoding.UTF8.GetBytes(
            "[" + string.Join(",", ops.Select(o => o.ToCanonicalJson())) + "]");

    /// <summary>
    /// Split the ops a peer lacks into batches that each encode below <see cref="MaxResponseBytes"/>.
    /// <para>
    /// Ops are kept in their given (deterministic dot) order, so a peer that receives only the first
    /// batches has a PREFIX and converges monotonically over successive intervals. A single op larger
    /// than the batch limit still gets its own batch — refusing to send it at all would strand it
    /// forever, and the transport's own guard is the backstop for a genuinely unsendable frame.
    /// </para>
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<FederationOp>> BatchResponses(IReadOnlyList<FederationOp> ops)
    {
        var batches = new List<IReadOnlyList<FederationOp>>();
        var current = new List<FederationOp>();
        int size = 2;   // the enclosing "[]"

        foreach (var op in ops)
        {
            int cost = System.Text.Encoding.UTF8.GetByteCount(op.ToCanonicalJson()) + 1; // + separator
            if (current.Count > 0 && size + cost > MaxResponseBytes)
            {
                batches.Add(current);
                current = new List<FederationOp>();
                size = 2;
            }
            current.Add(op);
            size += cost;
        }

        if (current.Count > 0) batches.Add(current);
        return batches;
    }

    /// <summary>Read the ops out of a pull response.</summary>
    public static IReadOnlyList<FederationOp> DecodeResponse(byte[] bytes)
    {
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(text);
        var ops = new List<FederationOp>();
        foreach (var el in doc.RootElement.EnumerateArray())
            ops.Add(FederationOp.FromJson(el.GetRawText()));
        return ops;
    }
}

/// <summary>
/// The fold acknowledgement (FR-009, added after the round-2 review found SC-001 unprovable).
///
/// WHAT WAS WRONG. The SC-001 measurement timed a local append and a socket write. `PushAsync`
/// swallows a send failure by design — the pull is its repair path — so the elapsed figure was
/// achievable with the peer switched off, and nothing anywhere read the peer's fold. SC-001 could
/// therefore be recorded as MEASURED without any evidence that the claim became visible remotely,
/// which is the single criterion SC-001 exists to establish.
///
/// FR-009 requires an operation to APPEAR IN THE PEER'S FOLD. That is a fact about the peer, and
/// the only party who can attest to it is the peer. So a receiver that folds an inbound board op
/// says so, naming the dot it folded, and the sender can wait for that rather than for its own
/// write to return.
///
/// The ack is advisory to convergence — losing one costs nothing, because the pull leg still
/// repairs — but it is REQUIRED for the acceptance measurement, which is exactly the right split:
/// the protocol does not depend on it, and the evidence does.
/// </summary>
public static class AckProtocol
{
    /// <summary>Box carrying "I folded this dot".</summary>
    public const string Box = "board-ack";

    public static byte[] Encode(Dot opId) =>
        System.Text.Encoding.UTF8.GetBytes(
            "{\"peer\":" + JsonSerializer.Serialize(opId.PeerName) + ",\"counter\":" + opId.Counter + "}");

    public static Dot Decode(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        var r = doc.RootElement;
        return new Dot(r.GetProperty("peer").GetString()!, r.GetProperty("counter").GetInt64());
    }
}

/// <summary>
/// The capability handshake (FR-018, added after codex found the push path ungated).
///
/// A peer MUST declare term-space awareness before ANY of its board operations are folded. The
/// default is fail-closed: a peer that has not said so is refused, because "we have not heard"
/// and "they are not aware" demand the same conservative answer when the mistake is irreversible.
/// </summary>
public static class HelloProtocol
{
    /// <summary>Box carrying a peer's declared capabilities.</summary>
    public const string Box = "hello";

    public static byte[] Encode(PeerCapabilities caps) =>
        System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            term_space_aware = caps.TermSpaceAware,
            space_id = caps.AdvertisedSpaceId,
        }));

    public static PeerCapabilities Decode(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        var r = doc.RootElement;
        return new PeerCapabilities(
            r.TryGetProperty("term_space_aware", out var a) && a.ValueKind == JsonValueKind.True,
            r.TryGetProperty("space_id", out var sp) && sp.ValueKind == JsonValueKind.String
                ? sp.GetString() : null);
    }
}
