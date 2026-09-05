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
    private int _pumpsStarted;

    /// <summary>
    /// How many background pump threads this carrier has ever started. Exposed to the test
    /// assembly because "Open() is idempotent" is otherwise unfalsifiable from outside: managed
    /// thread names are not enumerable, and the previous idempotence test asserted nothing at all.
    /// </summary>
    internal int PumpsStarted => Volatile.Read(ref _pumpsStarted);

    /// <summary>True while the background pump thread is still running.</summary>
    internal bool PumpAlive => _pump?.IsAlive ?? false;

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
    /// Asked, after delivery, whether the message is now DURABLY recorded. Returning false leaves
    /// the frame in the inbox to be retried on the next sweep - the frame is the only copy until
    /// something durable holds it. Null means "no durability owner", which is correct for an
    /// in-memory plane and preserves the previous behaviour.
    /// </summary>
    public Func<YnetMessage, bool>? ConfirmDurable { get; set; }

    /// <summary>Frames delivered but NOT confirmed durable, and therefore left for a retry.</summary>
    public long UndurableRetained => Interlocked.Read(ref _undurable);

    private long _undurable;

    private void NoteUndurable(string name)
    {
        Interlocked.Increment(ref _undurable);
        _ = name;   // retained deliberately: the file itself is the record, it is still in the inbox
    }

    /// <summary>
    /// A3: every non-frame file seen in the inbox, BY NAME. Silence here is a measurement, not an
    /// assumption — an empty list means the inbox was read and held only frames.
    /// </summary>
    public IReadOnlyList<string> StrayFiles
    {
        get { lock (_strays) return _strays.ToArray(); }
    }

    // Open/Close/PollOnce are serialized on this. `volatile bool` made _open VISIBLE across
    // threads but did not make check-then-set ATOMIC: two threads could both see _open == false,
    // both start a pump, and one would overwrite _cts/_pump so that Close() stopped only the
    // survivor while the orphan delivered forever. Found by adversarial review 2026-09-05.
    private readonly Lock _gate = new();

    public void Open()
    {
        lock (_gate)
        {
            if (_open) return;                             // idempotent, per IYnetInbound
            Directory.CreateDirectory(InboxDirectory);
            Directory.CreateDirectory(ProcessedDirectory);
            _open = true;

            if (_interval <= TimeSpan.Zero) return;        // manual mode: the caller pumps

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _pump = new Thread(() => Pump(token))
            {
                IsBackground = true,
                Name = "ynet-coop-carrier",
            };
            _pumpsStarted++;
            _pump.Start();
        }
    }

    public void Close()
    {
        Thread? pump;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            if (!_open) return;                            // idempotent
            _open = false;
            cts = _cts;
            pump = _pump;
            _cts = null;
            _pump = null;
        }

        if (cts is null) return;                           // manual mode: nothing to stop
        cts.Cancel();
        // Joined OUTSIDE the gate: the pump takes the gate in PollOnce, so joining while holding
        // it would deadlock the very shutdown it is meant to complete.
        pump?.Join(TimeSpan.FromSeconds(5));
        cts.Dispose();
    }

    public void Dispose() => Close();

    private void Pump(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                PollOnceCore();
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
        // The two modes are mutually exclusive, and saying so in a comment did not make it true:
        // a caller could invoke PollOnce while the background pump was running, both could
        // enumerate and DELIVER the same frame before either moved it to processed/, and the
        // message was raised TWICE. The comment claimed the duplicate was structurally impossible.
        // It is now actually refused. Found by adversarial review 2026-09-05.
        if (_interval > TimeSpan.Zero && _open)
            throw new InvalidOperationException(
                "PollOnce() is for MANUAL mode only. This carrier is running a background pump, and " +
                "sweeping the same inbox from two threads delivers a frame twice. Construct it with " +
                "CoopFileInbound.Manual(...) to drive it yourself.");

        return PollOnceCore();
    }

    private int PollOnceCore()
    {
        // Serialized so two manual callers - or a manual caller and the pump during shutdown -
        // cannot interleave a delivery with the move that makes it exactly-once.
        lock (_gate)
        {
            return Sweep();
        }
    }

    private int Sweep()
    {
        Directory.CreateDirectory(InboxDirectory);
        Directory.CreateDirectory(ProcessedDirectory);

        var delivered = 0;
        foreach (var path in Directory.EnumerateFiles(InboxDirectory).OrderBy(p => p, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);

            // A3: seen and NAMED, never silently skipped. A non-frame file, a `.frame` that is not
            // valid JSON, and a `.frame` that parses but carries no identity are all strays -
            // `{}` parses cleanly into a record of empty defaults, and delivering that as
            // "unknown-origin" manufactured a message out of an empty file.
            var frame = Classify(path);
            if (frame is null)
            {
                NoteStray(name);
                continue;
            }

            var message = new YnetMessage(
                MessageId: Path.GetFileNameWithoutExtension(name),
                Origin: frame.Origin,
                Summary: $"{frame.Signal}: {frame.Body}",
                Body: Encoding.UTF8.GetBytes(frame.Body));

            Received?.Invoke(message);

            // THE FRAME IS THE ONLY COPY UNTIL THE ALERT IS DURABLE.
            //
            // Received only ENQUEUES onto the receiver machine's mailbox; the spool write happens
            // later, on the machine's own thread. Consuming here regardless meant that a spool
            // write which failed - disk full, permissions, a lock timeout - or a process that died
            // in between, lost the message from BOTH places: the frame was already out of the
            // inbox and could never be retried. On the bounded-mailbox OVERFLOW path the loss was
            // not even a race, it was certain: Post refuses, nothing is ever recorded under this
            // message id, and the carrier moved the frame anyway.
            //
            // So the frame stays where it is until the owner of durability says the record exists.
            // A carrier with no gate keeps the old behaviour, which is correct for the loopback
            // plane where there is no file to lose. Found by adversarial review 2026-09-05.
            if (ConfirmDurable is not null && !ConfirmDurable(message))
            {
                NoteUndurable(name);
                continue;                                   // left in the inbox; next sweep retries
            }

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

    /// <summary>
    /// The most stray names retained. A producer creating uniquely-named junk would otherwise grow
    /// this list for the life of the process. The COUNT stays exact (StrayCount) because that is
    /// what tells a lane it is being mis-addressed; only the NAMES are capped.
    /// </summary>
    private const int MaxRetainedStrayNames = 256;

    /// <summary>Total non-frame files seen, including any whose names were not retained.</summary>
    public long StrayCount => Interlocked.Read(ref _strayCount);

    private long _strayCount;

    private void NoteStray(string name)
    {
        lock (_strays)
        {
            if (_strays.Contains(name)) return;
            Interlocked.Increment(ref _strayCount);
            if (_strays.Count < MaxRetainedStrayNames) _strays.Add(name);
        }
    }

    /// <summary>One non-consuming look at the inbox: how many deliverable frames, and what else.</summary>
    /// <remarks>
    /// Shares <see cref="Classify"/> with the sweep on purpose. `doctor` previously classified by
    /// FILENAME SUFFIX alone, so a `.frame` holding invalid JSON - or `{}` - was reported as a
    /// waiting frame and doctor exited 0, while the receiver would treat it as a stray. Two
    /// classifiers meant the diagnostic disagreed with the thing it was diagnosing.
    /// </remarks>
    public (int Frames, IReadOnlyList<string> Strays) Inspect()
    {
        if (!Directory.Exists(InboxDirectory)) return (0, Array.Empty<string>());

        var frames = 0;
        var strays = new List<string>();
        foreach (var path in Directory.EnumerateFiles(InboxDirectory).OrderBy(p => p, StringComparer.Ordinal))
        {
            if (Classify(path) is null) strays.Add(Path.GetFileName(path));
            else frames++;
        }
        return (frames, strays);
    }

    /// <summary>Read one inbox file as a frame, or null when it is not a deliverable frame.</summary>
    private static YnetFrame? Classify(string path)
    {
        if (!Path.GetFileName(path).EndsWith(".frame", StringComparison.Ordinal)) return null;
        try
        {
            var frame = JsonSerializer.Deserialize<YnetFrame>(File.ReadAllText(path));
            return frame is not null && IsAddressed(frame) ? frame : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// A frame must say who sent it. Missing identity is refused rather than defaulted: a frame
    /// that cannot be attributed cannot be acted on, and inventing "unknown-origin" for it hides
    /// a malformed producer behind a plausible-looking message.
    /// </summary>
    private static bool IsAddressed(YnetFrame f)
    {
        if (string.IsNullOrWhiteSpace(f.Origin)
            || string.IsNullOrWhiteSpace(f.SenderNode)
            || string.IsNullOrWhiteSpace(f.SenderActor))
            return false;

        // Origin is what every downstream consumer attributes the message to, and SenderNode /
        // SenderActor are what a verifier would key on. Accepting a frame whose Origin disagrees
        // with them let a sender claim ANY origin it liked - Origin="victim/victim.actor" with
        // SenderNode="attacker" was delivered, and displayed, as coming from the victim. Requiring
        // them to agree makes the attribution one field instead of two that can disagree silently.
        // Found by adversarial review 2026-09-05.
        return string.Equals(f.Origin, $"{f.SenderNode}/{f.SenderActor}", StringComparison.Ordinal);
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
