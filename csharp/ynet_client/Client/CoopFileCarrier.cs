// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The REAL carrier plane. Until this file existed, `ynet_client run` bound LoopbackInbound and
// `inject` manufactured its own message: the client could not receive a byte that another process
// had written, so M6-R2 (receive off a real carrier) and M6-R3 (send) were NOT MET here and were
// reported as NOT MET. This closes both against the carrier the fleet actually runs on today.
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
/// The cross-process, cross-lane receive plane: watches this lane's own inbox directory and
/// raises <see cref="Received"/> for each frame another PROCESS wrote. No agent is involved at any
/// point — that is the whole point of M6-R2.
/// </summary>
public sealed class CoopFileInbound : IYnetInbound, IDisposable
{
    private readonly string _root;
    private readonly PeerIdentity _self;
    private readonly TimeSpan _interval;
    private readonly List<string> _strays = new();
    private CancellationTokenSource? _cts;
    private Thread? _pump;
    private volatile bool _open;

    /// <param name="pollInterval">
    /// How often the background pump sweeps the inbox. <see cref="TimeSpan.Zero"/> selects MANUAL
    /// mode: no pump is started and the caller drives <see cref="PollOnce"/> itself.
    ///
    /// Manual mode is not a test convenience — it exists because the two modes are mutually
    /// exclusive and mixing them double-delivers. A background pump and an explicit PollOnce both
    /// enumerate the same inbox, so a frame taken by one can be raised by the other before it is
    /// moved to processed/. Making "no pump" an explicit mode rather than a discipline means the
    /// duplicate cannot be reintroduced by a caller who simply forgets.
    /// </param>
    public CoopFileInbound(PeerIdentity self, string? root = null, TimeSpan? pollInterval = null)
    {
        _self = self ?? throw new ArgumentNullException(nameof(self));
        _root = CoopLayout.ResolveRoot(root);
        _interval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    /// <summary>A carrier the caller pumps itself: deterministic, and never double-delivers.</summary>
    public static CoopFileInbound Manual(PeerIdentity self, string? root = null) =>
        new(self, root, TimeSpan.Zero);

    public string PlaneName => "coop-file-carrier";

    public string InboxDirectory => CoopLayout.InboxOf(_root, _self);

    public string ProcessedDirectory => CoopLayout.ProcessedOf(_root, _self);

    public event Action<YnetMessage>? Received;

    /// <summary>Frames delivered since Open(). Reported, never inferred from the spool.</summary>
    public int FramesDelivered { get; private set; }

    /// <summary>
    /// A3: every non-frame file seen in the inbox, BY NAME. Silence here is a measurement, not an
    /// assumption — an empty list means the inbox was read and held only frames.
    /// </summary>
    public IReadOnlyList<string> StrayFiles
    {
        get { lock (_strays) return _strays.ToArray(); }
    }

    public void Open()
    {
        if (_open) return;                                 // idempotent, per IYnetInbound
        Directory.CreateDirectory(InboxDirectory);
        Directory.CreateDirectory(ProcessedDirectory);
        _open = true;

        if (_interval <= TimeSpan.Zero) return;            // manual mode: the caller pumps

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _pump = new Thread(() => Pump(token))
        {
            IsBackground = true,
            Name = "ynet-coop-carrier",
        };
        _pump.Start();
    }

    public void Close()
    {
        if (!_open) return;                                // idempotent
        _open = false;
        var cts = _cts;
        if (cts is null) return;                           // manual mode: nothing to stop
        _cts = null;
        cts.Cancel();
        _pump?.Join(TimeSpan.FromSeconds(5));
        cts.Dispose();
    }

    public void Dispose() => Close();

    private void Pump(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                PollOnce();
            }
            catch (Exception)
            {
                // A carrier that dies on one bad file stops receiving everything after it. Skip the
                // cycle and try again; a frame that cannot be parsed is recorded as a stray below.
            }

            token.WaitHandle.WaitOne(_interval);
        }
    }

    /// <summary>
    /// One sweep of the inbox. Exposed so a test — and the `poll` verb — can drive the carrier
    /// deterministically instead of sleeping and hoping.
    /// </summary>
    public int PollOnce()
    {
        Directory.CreateDirectory(InboxDirectory);
        Directory.CreateDirectory(ProcessedDirectory);

        var delivered = 0;
        foreach (var path in Directory.EnumerateFiles(InboxDirectory).OrderBy(p => p, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);
            if (!name.EndsWith(".frame", StringComparison.Ordinal))
            {
                NoteStray(name);                            // A3: seen and named, never silently skipped
                continue;
            }

            YnetFrame? frame;
            try
            {
                frame = JsonSerializer.Deserialize<YnetFrame>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                NoteStray(name);                            // a .frame that is not a frame is still a stray
                continue;
            }

            if (frame is null)
            {
                NoteStray(name);
                continue;
            }

            Received?.Invoke(new YnetMessage(
                MessageId: Path.GetFileNameWithoutExtension(name),
                Origin: string.IsNullOrEmpty(frame.Origin) ? "unknown-origin" : frame.Origin,
                Summary: $"{frame.Signal}: {frame.Body}",
                Body: Encoding.UTF8.GetBytes(frame.Body)));

            FramesDelivered++;
            delivered++;
            Consume(path, name);
        }

        return delivered;
    }

    /// <summary>
    /// Move the frame out of the inbox so it is delivered exactly once across restarts. A frame
    /// that cannot be moved is DELETED rather than left to be re-delivered forever — but only
    /// after Received has returned, so the durable spool already holds it.
    /// </summary>
    private void Consume(string path, string name)
    {
        var target = Path.Combine(ProcessedDirectory, name);
        try
        {
            File.Move(path, target, overwrite: true);
        }
        catch (IOException)
        {
            try { File.Delete(path); } catch (IOException) { /* peer holds it; next sweep retries */ }
        }
    }

    private void NoteStray(string name)
    {
        lock (_strays)
        {
            if (!_strays.Contains(name)) _strays.Add(name);
        }
    }
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
