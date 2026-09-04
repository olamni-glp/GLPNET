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

/// <summary>Wire-serialisable causal frontier: peer -> highest contiguously-known counter.</summary>
public static class FrontierCodec
{
    /// <summary>Canonical JSON, peers in ordinal order so two hosts produce identical bytes.</summary>
    public static string Encode(VersionVector v)
    {
        var pairs = v.Peers.Select(p => $"{JsonSerializer.Serialize(p)}:{v[p]}");
        return "{" + string.Join(",", pairs) + "}";
    }

    public static VersionVector Decode(string json)
    {
        var vv = new VersionVector();
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
            vv = vv.With(new Dot(prop.Name, prop.Value.GetInt64()));
        return vv;
    }
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

    /// <summary>Build a pull request carrying this host's frontier.</summary>
    public static byte[] EncodeRequest(VersionVector frontier) =>
        System.Text.Encoding.UTF8.GetBytes(FrontierCodec.Encode(frontier));

    /// <summary>Read the frontier out of a pull request.</summary>
    public static VersionVector DecodeRequest(byte[] bytes) =>
        FrontierCodec.Decode(System.Text.Encoding.UTF8.GetString(bytes));

    /// <summary>Build a pull response carrying only the ops the requester lacks.</summary>
    public static byte[] EncodeResponse(IReadOnlyList<FederationOp> ops) =>
        System.Text.Encoding.UTF8.GetBytes(
            "[" + string.Join(",", ops.Select(o => o.ToCanonicalJson())) + "]");

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
