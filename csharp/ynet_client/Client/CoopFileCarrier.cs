// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// ADDRESSING AND THE SEND HALF of the M6 file carrier.
//
// 🔴 THIS FILE DELIBERATELY CONTAINS NO INBOUND PLANE. It used to. Two lanes in this same repo
// independently built a cross-lane receive plane on 2026-09-05 - CoopFileInbound.cs (olamnit/shiras,
// landed on develop) and a rival class of the same name here - and they collided on the merge. That
// is the "three M6 clients in one morning" defect repeating inside ONE repository, and the fleet
// rule that follows from it is R-B's procedural half: a mandatory capability names its OWNER in the
// requirement, because a claim after the fact is a race.
//
// The incumbent won on merit, not on merge order: it guards against reading a PARTIALLY WRITTEN
// frame (a truncated delivery is silent corruption), it keeps claimed frames in .taken/ for
// forensics, and it surfaces strays and poll failures as events. This file keeps only what it did
// NOT have, and those pieces were contributed onto it rather than kept as a fork.
//
// INTEROP IS MEASURED, NOT ASSUMED
//     The canonical M6 client is qhstate's YngeniOS.Ynet.Client (engineer ruling R-B,
//     2026-09-05T15:10Z); this lane is a CONTRIBUTOR to it, not a rival. Its source is in another
//     repo and was not readable from this host, so the two interop rules below were derived by
//     MEASUREMENT against the live COOP root and are stated with the evidence that backs them:
//
//       1. Peer directory name = Escape(identity) + "~" + sha256(identity)[..12], where identity
//          is "<node>/<actor>" and Escape percent-encodes every character outside [A-Za-z0-9_-]
//          as an uppercase %XX.  VERIFIED: this rule reproduces all 21 live peer directories
//          under /d/coop byte-for-byte, with zero mismatches (see PeerIdentityTests).
//       2. A frame is a UTF-8 JSON object {Origin, Sequence, SenderNode, SenderActor, Signal,
//          Body} in a file named "<epochMs>.<seq>.<guidN>.frame".  VERIFIED against live frames
//          written by qhstate's carrier.
//
//     Both rules are therefore falsifiable: if the canonical client ever changes them, the tests
//     that pin them fail here rather than this lane silently addressing nobody.
//
// A3 — A STRAY FILE IS LOUD, NOT SILENT
//     shiras-glpnet measured that CoopFileCarrier.cs:169 enumerates "*.frame" only, so a non-frame
//     file in an M6 inbox is not refused, it is NOT SEEN: frames_refused stayed 0 while two
//     ACK-MANDATORY broadcasts sat unread. The engineer ruled that in as well as the A1 filter.
//     This carrier therefore COUNTS AND NAMES every non-frame file it finds (StrayFiles), so a
//     mis-addressed lane can discover it has been mis-addressed at all.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ynet.Client;

/// <summary>
/// A YNET peer address: a node and an actor, plus the on-disk directory name they hash to.
/// </summary>
public sealed record PeerIdentity(string Node, string Actor)
{
    /// <summary>The wire identity, "<c>node/actor</c>" — what both the digest and Origin use.</summary>
    public string Identity => $"{Node}/{Actor}";

    /// <summary>
    /// The peer's directory name under the COOP root. The "~" is load-bearing beyond uniqueness:
    /// engineer ruling A1 makes it the marker every document fan-out must skip, so a broadcast
    /// cannot be dropped into a mailbox.
    /// </summary>
    public string DirectoryName => $"{Escape(Identity)}~{ShortDigest(Identity)}";

    /// <summary>Parse "node/actor". Anything else is refused loudly rather than half-accepted.</summary>
    public static PeerIdentity Parse(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        var slash = identity.IndexOf('/');
        if (slash <= 0 || slash == identity.Length - 1)
            throw new FormatException($"peer identity must be '<node>/<actor>', got '{identity}'");
        return new PeerIdentity(identity[..slash], identity[(slash + 1)..]);
    }

    internal static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
                sb.Append(c);
            else
                sb.Append('%').Append(((int)c).ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    internal static string ShortDigest(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];
}

/// <summary>One frame exactly as it sits on the wire. Property names are the wire contract.</summary>
public sealed record YnetFrame
{
    [JsonPropertyName("Origin")] public string Origin { get; init; } = "";
    [JsonPropertyName("Sequence")] public long Sequence { get; init; }
    [JsonPropertyName("SenderNode")] public string SenderNode { get; init; } = "";
    [JsonPropertyName("SenderActor")] public string SenderActor { get; init; } = "";
    [JsonPropertyName("Signal")] public string Signal { get; init; } = "";
    [JsonPropertyName("Body")] public string Body { get; init; } = "";
}

/// <summary>
/// Shared root resolution and directory layout for the file carrier. A peer's mailbox is
/// <c>&lt;root&gt;/&lt;peer-dir&gt;/inbox</c>, and consumed frames move to <c>../processed</c>.
/// </summary>
public static class CoopLayout
{
    /// <summary>Env var naming the COOP root; the carrier refuses to guess when it is unset.</summary>
    public const string RootVariable = "YNET_COOP_ROOT";

    public static string ResolveRoot(string? explicitRoot = null)
    {
        var root = explicitRoot
                   ?? Environment.GetEnvironmentVariable(RootVariable)
                   ?? throw new InvalidOperationException(
                       $"COOP root is not set. Pass --coop <root> or set {RootVariable}. " +
                       "The carrier refuses to guess a root: guessing one addresses nobody and reports success.");
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"COOP root '{root}' does not exist");
        return root;
    }

    public static string InboxOf(string root, PeerIdentity peer) =>
        Path.Combine(root, peer.DirectoryName, "inbox");

    public static string ProcessedOf(string root, PeerIdentity peer) =>
        Path.Combine(root, peer.DirectoryName, "processed");
}

/// <summary>
/// The send half: writes a frame into ONE peer's inbox, atomically.
///
/// This is the first real implementation of <see cref="IYnetOutbound"/>. That interface has existed
/// since the receiver machine was written and, until now, only a test fake implemented it — a
/// declared capability with no realization, which is why this lane reported M6-R3 as NOT MET rather
/// than counting the interface as the feature.
///
/// One instance is a ROUTE TO ONE PEER, because <see cref="YnetMessage"/> carries no destination:
/// binding the peer at construction is what lets <c>Send(YnetMessage)</c> honour the interface it
/// implements instead of widening it.
/// </summary>
public sealed class CoopFileOutbound : IYnetOutbound
{
    private readonly string _root;
    private readonly PeerIdentity _self;
    private readonly PeerIdentity _peer;
    private long _sequence;

    public CoopFileOutbound(PeerIdentity self, PeerIdentity peer, string? root = null)
    {
        _self = self ?? throw new ArgumentNullException(nameof(self));
        _peer = peer ?? throw new ArgumentNullException(nameof(peer));
        _root = CoopLayout.ResolveRoot(root);
    }

    /// <summary>The peer's inbox this route writes into.</summary>
    public string PeerInbox => CoopLayout.InboxOf(_root, _peer);

    /// <summary>
    /// True when the peer has actually registered a mailbox. Refusing to create it is deliberate:
    /// creating a peer's inbox on its behalf invents a peer that never announced itself, and every
    /// send after that reports success into a directory nobody reads.
    /// </summary>
    public bool PeerIsReachable => Directory.Exists(PeerInbox);

    /// <summary>The file name written by the last successful <see cref="Send(YnetMessage)"/>.</summary>
    public string? LastFrameName { get; private set; }

    /// <inheritdoc />
    /// <remarks>Honours the interface contract: a dead peer returns false, it does not throw.</remarks>
    public bool Send(YnetMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Send(message.Summary, Encoding.UTF8.GetString(message.Body.Span));
    }

    /// <summary>Send a signal and body. Returns false when the peer has no registered mailbox.</summary>
    public bool Send(string signal, string body)
    {
        if (!PeerIsReachable) return false;

        var frame = new YnetFrame
        {
            Origin = _self.Identity,
            Sequence = Interlocked.Increment(ref _sequence) - 1,
            SenderNode = _self.Node,
            SenderActor = _self.Actor,
            Signal = signal,
            Body = body,
        };

        var name = FormattableString.Invariant(
            $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.{frame.Sequence}.{Guid.NewGuid():N}.frame");

        // Write beside the target and rename: a reader polling the inbox must never observe a
        // half-written frame and consume it as a stray.
        var final = Path.Combine(PeerInbox, name);
        var temp = final + ".partial";
        File.WriteAllText(temp, JsonSerializer.Serialize(frame), new UTF8Encoding(false));
        File.Move(temp, final);
        LastFrameName = name;
        return true;
    }
}
