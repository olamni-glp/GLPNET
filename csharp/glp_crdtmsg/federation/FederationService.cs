// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// The federation service (feature 102, T031-T035).
//
// Contract federation-wire.md W1/W4/W5/W7 / FR-001, FR-003, FR-004, FR-028, FR-030.
//
// TWO CONVERGENCE LEGS, AND BOTH ARE REQUIRED (ruling Q-GLPNETG28-03):
//   push  — on append, 5 s steady-state target.
//   pull  — every 60 s, exchanging version vectors FIRST and transferring only what the peer lacks.
// Push alone loses any op in flight across a dropped link, with no repair, leaving two boards
// silently divergent — the worst possible failure for a board whose entire purpose is agreement.
// Pull alone is self-healing but a 60 s window is longer than the time a lane takes to start
// duplicate work, which is the exact defect User Story 1 names.
//
// APPEND LOCALLY, THEN SHIP (FR-030). Never the reverse: a federation that ships an op it has not
// stored loses that op whenever the link succeeds and the local write does not.

using System.Net;
using System.Text;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Route;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>The abstract link this service needs. Lets the fold and the legs be tested without QUIC.</summary>
public interface IFederationLink : IAsyncDisposable
{
    string LocalPeer { get; }
    IPEndPoint? ListenEndPoint { get; }
    Task ListenAsync(IPEndPoint bind, CancellationToken ct = default);
    Task ConnectPeerAsync(string peerName, IPEndPoint remote, CancellationToken ct = default);
    ValueTask SendAsync(string toPeer, string box, ReadOnlyMemory<byte> bytes, CancellationToken ct = default);
    System.Threading.Channels.ChannelReader<LinkInbound> Inbound { get; }
}

/// <summary>Adapts the existing, unchanged <see cref="QuicLinkTransport"/> to <see cref="IFederationLink"/>.</summary>
public sealed class QuicFederationLink : IFederationLink
{
    private readonly QuicLinkTransport _t;
    public QuicFederationLink(QuicLinkTransport t) => _t = t;
    public string LocalPeer => _t.LocalPeer;
    public IPEndPoint? ListenEndPoint => _t.ListenEndPoint;
    public Task ListenAsync(IPEndPoint bind, CancellationToken ct = default) => _t.ListenAsync(bind, ct);
    public Task ConnectPeerAsync(string p, IPEndPoint r, CancellationToken ct = default) => _t.ConnectPeerAsync(p, r, ct);
    public ValueTask SendAsync(string p, string box, ReadOnlyMemory<byte> b, CancellationToken ct = default) => _t.SendAsync(p, box, b, ct);
    public System.Threading.Channels.ChannelReader<LinkInbound> Inbound => _t.Inbound;
    public ValueTask DisposeAsync() => _t.DisposeAsync();
}

/// <summary>Why federation is not fully operational, when it is not (FR-004 / contract W7).</summary>
public enum FederationHealth
{
    /// <summary>Disabled in configuration. Local lanes are served normally.</summary>
    Disabled,

    /// <summary>Bound and pushing to at least one admitted peer.</summary>
    Federating,

    /// <summary>
    /// Enabled but no peer is reachable. Local lanes are served UNCHANGED and this is reported
    /// EXPLICITLY — it is never reported as success.
    /// </summary>
    DegradedLocalOnly,
}

/// <summary>
/// Binds a listener, dials peers by literal address, and runs both convergence legs over the
/// existing board fold.
/// </summary>
public sealed class FederationService : IAsyncDisposable
{
    /// <summary>The box federated board operations travel on.</summary>
    public const string BoardBox = "board";

    /// <summary>
    /// The most a single tail tick will read from one log. The tail catches up over successive
    /// ticks rather than materialising an arbitrarily large board in one allocation.
    /// </summary>
    public const int MaxTailChunkBytes = 4 * 1024 * 1024;

    /// <summary>
    /// The largest single record the tail will assemble. Above the transport's own 64 MiB frame
    /// guard, so anything that could legitimately have crossed the wire can also be tailed.
    /// </summary>
    public const long MaxTailRecordBytes = 96L * 1024 * 1024;

    private readonly FederationConfig _config;
    private readonly IFederationLink _link;
    private readonly FederationFold _fold;
    private readonly PeerSet _peers;
    private readonly IBoardLog _log;
    // CONCURRENT BY TYPE, not by convention. Four loops (receive, pull, board tail, heartbeat)
    // run at once and all of these are read and mutated from more than one of them. A plain
    // Dictionary faults when enumerated during a mutation, and a faulted background loop stops
    // converging without saying so — the failure mode this whole feature exists to eliminate.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _admitted = new(StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, PeerCapabilities> _peerCaps = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, string> _originKeys;
    private readonly TimeProvider _clock;

    /// <summary>
    /// Node ids whose link is believed live. A peer DROPS OUT of this on a send failure, which is
    /// what makes the pull loop re-dial it. Distinct from <see cref="_admitted"/>, which records
    /// that a peer has EVER completed mutual verification — a different fact, and the one the status
    /// surface reports.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _connected = new(StringComparer.Ordinal);

    private bool _bound;
    private bool _opCrossed;

    // Per-peer, because "was that crossing on this machine?" is a question ABOUT A PEER. A single
    // shared field answered it with whichever peer was dialled last (R3-05).
    private readonly object _machineGate = new();
    private readonly Dictionary<string, Tri> _sameMachineByPeer = new(StringComparer.Ordinal);
    private readonly HashSet<string> _crossedFrom = new(StringComparer.Ordinal);
    private PolicyRefusal? _policyRefusal;
    private CancellationTokenSource? _pumpCts;

    public FederationService(FederationConfig config, IFederationLink link, FederationFold fold,
                             IBoardLog log, TimeProvider? clock = null)
    {
        _config = config;
        _link = link;
        _fold = fold;
        _peers = config.ToPeerSet();
        _log = log;
        _originKeys = _peers.ToSpkiTable();
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// When true, an operation whose origin cannot be cryptographically verified is REFUSED rather
    /// than folded. Default false, because a peer that has not published its public key is a
    /// deployment state, not an attack — but the state is reported (<see cref="UnverifiedOps"/>),
    /// never silently treated as verified.
    /// </summary>
    public bool RequireVerifiedAttribution { get; init; }

    /// <summary>
    /// This host's own node id and public key, so its OWN operations verify.
    /// <para>
    /// Without it, <c>require_verified_attribution</c> refused everything this host posted: the
    /// verifier table held only configured PEERS, so the tail and startup replay classified local
    /// operations as UnverifiedOrigin and dropped them. Turning the security setting on disabled
    /// the host's ability to publish at all — a gate that locks the door from the inside.
    /// </para>
    /// </summary>
    public void EnrolLocalIdentity(string nodeId, string spkiBase64)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(spkiBase64)) return;
        _localKeys[nodeId.Trim().ToLowerInvariant()] = spkiBase64.Trim();
    }

    private readonly Dictionary<string, string> _localKeys = new(StringComparer.Ordinal);

    /// <summary>The origin key for a node id: a configured peer's, or this host's own.</summary>
    private string? KeyForOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return null;

        // NORMALISE BEFORE LOOKUP. Both tables are keyed ordinally on lowercase hex — as they must
        // be, since the transport compares ordinally — so an admitted peer that consistently
        // UPPERCASED a configured node id across origin, op_id.peer and term.host missed the lookup,
        // was classified UnverifiedOrigin, and (with strict attribution off) was folded with no
        // signature at all. A verification bypass by letter case.
        string key = origin.Trim();
        if (NodeIdentityStore.IsNodeId(key)) key = key.ToLowerInvariant();

        if (_originKeys.TryGetValue(key, out var peer)) return peer;
        return _localKeys.TryGetValue(key, out var mine) ? mine : null;
    }

    /// <summary>How many folded operations could not have their origin proven. For the status surface.</summary>
    public int UnverifiedOps { get; private set; }

    /// <summary>How many operations were REFUSED on attribution grounds. Counted, never hidden.</summary>
    public int RefusedOps { get; private set; }

    /// <summary>
    /// Raised when an operation is refused mid-batch. The caller reports it; the batch continues,
    /// because one bad operation must not strand every valid one behind it.
    /// </summary>
    public event Action<AttributionRefusedException>? OnRefusal;

    /// <summary>Where the serving process publishes its measured status, so a separate `status` can read it.</summary>
    public string? StatusHeartbeatPath { get; init; }

    public FederationFold Fold => _fold;
    public PeerSet Peers => _peers;

    /// <summary>Health, derived from what was MEASURED — never from configuration alone.</summary>
    public FederationHealth Health =>
        !_config.Enabled ? FederationHealth.Disabled
        // LIVE CONNECTIVITY, not history. _admitted records that a peer has EVER completed mutual
        // verification and is deliberately retained across a send failure — so reading health from
        // it meant a service whose last link had dropped went on reporting Federating, which is the
        // one thing FR-004 says must never happen: a degraded deployment reported as a working one.
        : _connected.Count > 0 ? FederationHealth.Federating
        : FederationHealth.DegradedLocalOnly;

    /// <summary>
    /// Bind the listener. A loopback bind while enabled is REFUSED, not accepted-and-warned: it is
    /// the failure mode that looks exactly like success (FR-001).
    /// </summary>
    public async Task<bool> BindAsync(CancellationToken ct = default)
    {
        if (!_config.Enabled) return false;

        var problems = _config.Validate();
        if (problems.Count > 0)
            throw new InvalidOperationException("federation config refused: " + string.Join("; ", problems));

        try
        {
            var addr = IPAddress.Parse(_config.BindAddress);
            await _link.ListenAsync(new IPEndPoint(addr, _config.BindPort), ct).ConfigureAwait(false);
            _bound = true;
            PublishStatus();
            return true;
        }
        catch (Exception ex) when (PolicyRefusal.Detect(ex) is { } refusal)
        {
            // FR-023: a host-policy refusal is its OWN named failure. It presents as a healthy build
            // and a passing suite followed by a daemon that never runs, so a generic error here
            // costs hours every time.
            _policyRefusal = refusal;
            _bound = false;
            // PUBLISH IT. `serve` exits after a failed bind telling the operator to run `status` —
            // and status is a different process. Without this the named FR-023 refusal died with
            // the daemon and status showed unknown, or worse, a still-fresh healthy heartbeat from
            // the previous run: the one failure mode this whole surface exists to name, unnameable.
            PublishStatus();
            return false;
        }
    }

    /// <summary>
    /// Dial a configured peer by LITERAL address (FR-003). A name that does not resolve is reported
    /// as <see cref="AdmissionOutcome.NameResolutionFailed"/> and NEVER as a transport failure —
    /// every host on this estate resolves to fe80:: link-local only.
    /// </summary>
    /// <param name="peer">
    /// The peer's NODE ID — the same string used as the transport's dial key, its hello value and
    /// its pin-table key. A human name is not accepted here; it is a label, not an identity.
    /// </param>
    public async Task<AdmissionOutcome> DialAsync(string peer, CancellationToken ct = default)
    {
        var entry = _peers.Find(peer) ?? _peers.Entries.FirstOrDefault(e => e.Name == peer);
        if (entry is null) return AdmissionOutcome.NotInPeerSet;
        if (entry.Endpoints.Count == 0) return AdmissionOutcome.NameResolutionFailed;

        // A participant may answer on several addresses; try each. Reaching it on the second does
        // not make it a second participant (SC-006).
        foreach (var ep in entry.Endpoints)
        {
            try
            {
                await _link.ConnectPeerAsync(entry.NodeId, ep, ct).ConfigureAwait(false);
                _admitted[entry.NodeId] = 0;
                _connected[entry.NodeId] = 0;
                // Declare our own capabilities so the peer's gate can admit us. A peer that never
                // hears this refuses our pushes — which is the correct fail-closed direction.
                await AnnounceCapabilitiesAsync(entry.NodeId, ct).ConfigureAwait(false);
                // KEYED BY PEER. A single field was overwritten by every successful dial, so with
                // two peers an op from a same-machine one inherited the last remote peer's "No" and
                // was reported as cross-host evidence — the precise thing FR-022 exists to prevent.
                // Three-valued: a probe that could not run is Unknown, never a measured No.
                lock (_machineGate)
                    _sameMachineByPeer[entry.NodeId] = FederationStatusProbe.SameMachineTri(
                        _link.ListenEndPoint?.Address ?? IPAddress.Any, ep.Address);
                PublishStatus();
                return AdmissionOutcome.Admitted;
            }
            catch (Exception ex) when (IsIdentityFailure(ex))
            {
                // FR-008: a pin mismatch and an unreachable host demand OPPOSITE responses, so they
                // are never folded into one generic error.
                //
                // Catching only AuthenticationException missed the CONCRETE path: QuicLinkTransport
                // throws CrdtMsgException when the far end's control hello claims a node id other
                // than the one dialled. That fell through to the generic catch, was retried against
                // the next address, and finally reported Unreachable — pointing the operator at the
                // network when the fault was an identity mismatch, the exact opposite diagnosis.
                return AdmissionOutcome.PinMismatch;
            }
            catch
            {
                // try the next address for this same participant
            }
        }
        return AdmissionOutcome.Unreachable;
    }

    /// <summary>
    /// Whether an exception is an IDENTITY failure rather than a reachability one. The transport
    /// signals the two differently and they demand opposite operator responses (FR-008).
    /// </summary>
    private static bool IsIdentityFailure(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is System.Security.Authentication.AuthenticationException) return true;
            // The transport's own refusals: an unpinned peer, a pin that does not match, or a hello
            // claiming a different name. All three are identity, none of them are reachability.
            if (e is Envelope.CrdtMsgException or InvalidOperationException
                && (e.Message.Contains("pin", StringComparison.OrdinalIgnoreCase)
                    || e.Message.Contains("claims", StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Append an operation and then ship it. THE ORDER IS THE POINT (FR-030): the local durable
    /// write happens first, so an op survives a crash between the two steps and is recovered by the
    /// pull backstop.
    /// </summary>
    public async Task AppendAndPushAsync(FederationOp op, CancellationToken ct = default)
    {
        // THROUGH THE SAME CRITICAL SECTION AS EVERY OTHER INGESTION PATH.
        //
        // The local path checked nothing and locked nothing: calling it twice with one dot appended
        // a SECOND journal record before Apply noticed the duplicate, and a CONFLICTING operation on
        // an existing dot was made durable before the conflict was thrown — poisoning an append-only
        // journal that has no delete. It could also race an inbound admission for the same dot.
        //
        // Ordering inside the section is unchanged and still the point (FR-030): durable first, then
        // visible. An op applied to the fold before a failing append is visible, eligible for
        // reconciliation, and — because the fold holds its dot — treated as a duplicate on retry, so
        // it would be served to peers from a host that never stored it.
        await _admitGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_fold.Contains(op.OpId))
            {
                var held = _fold.Operations.FirstOrDefault(o => o.OpId.Equals(op.OpId));
                if (held is not null && !string.Equals(held.ToCanonicalJson(), op.ToCanonicalJson(),
                                                       StringComparison.Ordinal))
                    throw new DotConflictException(op.OpId);   // NOTHING has been written
                return;                                        // already ours, already durable
            }

            await _log.AppendAsync(op, ct).ConfigureAwait(false);   // 1. durable
            _fold.Apply(op);                                        // 2. visible
        }
        finally { _admitGate.Release(); }

        if (!_config.Enabled || !_config.PushOnAppend) return;
        await PushAsync(op, ct).ConfigureAwait(false);           // 3. best effort
    }

    /// <summary>Push one op to every admitted peer. Best effort — a failure here is repaired by the pull.</summary>
    public async Task PushAsync(FederationOp op, CancellationToken ct = default)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(op.ToCanonicalJson());
        foreach (var entry in _peers.Entries)
        {
            if (!_admitted.ContainsKey(entry.NodeId)) continue;
            try { await _link.SendAsync(entry.NodeId, BoardBox, bytes, ct).ConfigureAwait(false); }
            catch
            {
                // Mark the link down so the pull loop RE-DIALS it. Swallowing the failure and
                // leaving the peer "connected" is what made a dropped link permanent: every later
                // send went into a connection that no longer existed and failed the same way.
                _connected.TryRemove(entry.NodeId, out _);
            }
        }
    }

    /// <summary>
    /// Receive one inbound frame and fold it. Returns true if the op was NEW; false on redelivery.
    /// Redelivery is certain on a retrying link and must never double-count (FR-010).
    /// </summary>
    public async Task<bool> ReceiveOneAsync(CancellationToken ct = default)
    {
        var inbound = await _link.Inbound.ReadAsync(ct).ConfigureAwait(false);

        // Reaching here at all means the transport completed mutual verification against the pin
        // table — an unpinned party cannot establish a connection. The PASSIVE side learns it has an
        // admitted peer HERE; without this, a pure listener reported "op received from peer: yes"
        // beside "peer admitted: no", the surface contradicting itself again.
        MarkAdmitted(inbound.FromPeer);

        switch (inbound.Box)
        {
            case PullProtocol.RequestBox:
                await AnswerPullAsync(inbound.FromPeer, PullProtocol.DecodeRequest(inbound.Bytes), ct)
                    .ConfigureAwait(false);
                return false;

            case PullProtocol.ResponseBox:
                // Use what the peer ACTUALLY DECLARED, never an assumed-true literal — passing a
                // hard-coded `true` here would have made the gate unfalsifiable on this path too.
                //
                // AND PASS THE SENDER. A pull response arrives with an authenticated FromPeer;
                // discarding it recorded a null crossing, so an operation recovered only via pull
                // reported "an op crossed" while leaving same_machine unknown even when that peer
                // HAD been measured — withholding valid cross-host evidence SC-001 depends on.
                await ReconcileAsync(PullProtocol.DecodeResponse(inbound.Bytes),
                                     _peerCaps.TryGetValue(inbound.FromPeer, out var rc)
                                         ? rc
                                         : new PeerCapabilities(TermSpaceAware: false, null),
                                     ct, inbound.FromPeer)
                    .ConfigureAwait(false);
                return false;

            case HelloProtocol.Box:
                // ANSWER ANY DECLARATION THAT IS NOT ITSELF AN ANSWER.
                //
                // Suppressing on "have we seen this peer before" was scoped to the PROCESS, so when
                // one peer restarted and the other did not, the survivor's cache still held it and
                // never answered the fresh declaration — leaving the restarted peer's fail-closed
                // gate refusing everything the survivor sent, permanently and silently.
                bool needsAnswer = !HelloProtocol.IsReply(inbound.Bytes);
                _peerCaps[inbound.FromPeer] = HelloProtocol.Decode(inbound.Bytes);

                // ANSWER ONCE, AND ONLY THE FIRST DECLARATION.
                //
                // The gate is fail-closed both ways, so a side that never declares its own
                // capabilities has every push and pull response refused BY THE OTHER SIDE — which
                // left federation permanently one-way whenever only one host could dial.
                //
                // But answering EVERY hello made two daemons volley control frames forever: A's
                // reply is a hello, which B answers with a hello, which A answers... The fix for a
                // one-way link became an infinite loop, and both surfaces still looked healthy.
                // Replying only to a peer's FIRST declaration terminates the exchange after exactly
                // one round trip, which is all the gate needs.
                if (needsAnswer)
                    await AnnounceCapabilitiesAsync(inbound.FromPeer, ct, isReply: true)
                        .ConfigureAwait(false);
                return false;

            case AckProtocol.Box:
                // A PEER has attested that it folded this dot. This is the only evidence that an op
                // became visible remotely — a local write returning proves nothing about the peer,
                // and PushAsync swallows send failures by design.
                lock (_ackGate)
                {
                    _acked.Add(AckProtocol.Decode(inbound.Bytes));
                    foreach (var w in _ackWaiters) w.TrySetResult();
                }
                // ATTRIBUTE THE CROSSING. An ack is a frame that genuinely crossed from this peer,
                // so the same-machine question is answerable for it. Recording the dot without the
                // peer left _crossedFrom empty, so SameMachine stayed null and the acceptance run's
                // required Tri.No assertion could not pass even after a successful remote fold.
                RecordCrossing(inbound.FromPeer);
                return false;

            case BoardBox:
                // FR-018 ON THE PUSH PATH — the PRIMARY delivery path.
                //
                // Found by codex: gating only ReconcileAsync left this route open, so a
                // non-term-space-aware peer could bypass the gate entirely by pushing, and
                // irreversibly merge prohibited terms. Gating the secondary path and not the
                // primary one is not a partial fix; it is no fix.
                //
                // FAIL CLOSED: a peer that has not declared its capabilities is refused. "We have
                // not heard from them" and "they are not aware" get the same conservative answer,
                // because the mistake is monotone and cannot be undone.
                var declared = _peerCaps.TryGetValue(inbound.FromPeer, out var c)
                    ? c
                    : new PeerCapabilities(TermSpaceAware: false, null);
                var pushVerdict = MergeGate.CanMerge(declared, localTermSpaceAware: true);
                if (!pushVerdict.Allowed)
                    throw new MergeRefusedException(
                        $"pushed board op from '{inbound.FromPeer}': {pushVerdict.Reason}");
                break;

            default:
                return false;
        }

        var op = FederationOp.FromJson(Encoding.UTF8.GetString(inbound.Bytes));
        bool isNew = await AdmitAndFoldAsync(op, ct).ConfigureAwait(false);

        // Attest that it is now in THIS host's fold. Sent for a redelivery too: the sender's ack may
        // have been the thing that was lost, and a silent second delivery would leave it waiting.
        if (inbound.Box == BoardBox) await SendAckAsync(inbound.FromPeer, op.OpId, ct).ConfigureAwait(false);

        _opCrossed = true;
        // The passive side learns of an op without learning where it came from. That is UNKNOWN,
        // not "no crossing observed" — and never silently No (FR-021/FR-022).
        RecordCrossing(inbound.FromPeer);
        PublishStatus();
        return isNew;
    }

    /// <summary>
    /// Check one inbound operation's ATTRIBUTION, then store it durably, then expose it in the fold.
    /// Every inbound path goes through here; there is deliberately no second way in.
    /// <para>
    /// THE ORDER MATTERS TWICE OVER. Attribution is checked before anything is written, so a forged
    /// op never reaches the log. The durable append then happens before <c>Apply</c>, so a failed
    /// write cannot leave the op visible-but-unstored — a state in which redelivery is classified as
    /// a duplicate, the append is never retried, and this host serves an operation it does not have.
    /// </para>
    /// </summary>
    private async Task<bool> AdmitAndFoldAsync(FederationOp op, CancellationToken ct)
    {
        // SERIALISED. The receive, pull and board-tail paths call this concurrently, and the
        // sequence check→append→apply is not atomic without it: two deliveries could both observe
        // Contains == false, BOTH append to the append-only log, and only then deduplicate in the
        // fold. Two conflicting same-dot operations could likewise both reach the journal before one
        // threw — permanently poisoning every future replay, on a log with no delete.
        await _admitGate.WaitAsync(ct).ConfigureAwait(false);
        try { return await AdmitAndFoldLockedAsync(op, ct).ConfigureAwait(false); }
        finally { _admitGate.Release(); }
    }

    private readonly SemaphoreSlim _admitGate = new(1, 1);

    /// <summary>
    /// Admit an operation that is ALREADY on disk — the tailed-record path.
    /// <para>
    /// A tailed record is by definition durable: it is being read from the log. Routing it through
    /// the appending path wrote a second copy of every operation `post` created, because in `serve`
    /// the log being tailed IS <c>_log</c>. The gate still runs; only the write is skipped.
    /// </para>
    /// </summary>
    private async Task<bool> AdmitAlreadyDurableAsync(FederationOp op, CancellationToken ct)
    {
        // THE SAME CRITICAL SECTION AS THE LIVE PATHS.
        //
        // A separate lock here did not exclude them at all: a live push and a tailed line for one
        // dot could BOTH observe it absent, and the live path could then append a duplicate — or a
        // conflicting operation — into an append-only journal that has no delete. Two locks
        // protecting one invariant is one lock too many.
        await _admitGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var attribution = OpAttribution.Check(op, KeyForOrigin(op.Origin));

            if (attribution.Verdict is AttributionVerdict.Inconsistent or AttributionVerdict.SignatureInvalid)
                throw new AttributionRefusedException(attribution);

            if (attribution.Verdict == AttributionVerdict.UnverifiedOrigin)
            {
                if (RequireVerifiedAttribution) throw new AttributionRefusedException(attribution);
                UnverifiedOps++;
            }

            return _fold.Apply(op);
        }
        finally { _admitGate.Release(); }
    }

    private async Task<bool> AdmitAndFoldLockedAsync(FederationOp op, CancellationToken ct)
    {
        // NOT a bare Contains() check. That early return bypassed FederationFold.Apply and the
        // canonical-content comparison inside it, so a peer sending a DIFFERENT operation on an
        // already-folded dot was treated as a redelivery — and then ACKED. Replicas kept different
        // operations permanently, because reconciliation compares dots too. The conflict check has
        // to be on the path that actually admits, not only on the one that folds locally.
        if (_fold.Contains(op.OpId))
        {
            var held = _fold.Operations.FirstOrDefault(o => o.OpId.Equals(op.OpId));
            if (held is not null && !string.Equals(held.ToCanonicalJson(), op.ToCanonicalJson(),
                                                   StringComparison.Ordinal))
                throw new DotConflictException(op.OpId);
            return false;   // a genuine redelivery — already durable, already visible
        }

        var attribution = OpAttribution.Check(op, KeyForOrigin(op.Origin));

        // An inconsistent or forged attribution is REFUSED outright, whatever the strictness setting.
        // Wrong attribution is a fault (FR-009), and term.host is the leadership tie-break — a forged
        // one is monotone and cannot be undone after the merge.
        if (attribution.Verdict is AttributionVerdict.Inconsistent or AttributionVerdict.SignatureInvalid)
            throw new AttributionRefusedException(attribution);

        if (attribution.Verdict == AttributionVerdict.UnverifiedOrigin)
        {
            if (RequireVerifiedAttribution) throw new AttributionRefusedException(attribution);
            UnverifiedOps++;   // COUNTED and reported, never silently treated as verified
        }

        await _log.AppendAsync(op, ct).ConfigureAwait(false);   // durable
        return _fold.Apply(op);                                 // then visible
    }

    private readonly object _ackGate = new();
    private readonly HashSet<Dot> _acked = new();
    private readonly List<TaskCompletionSource> _ackWaiters = new();

    /// <summary>Tell a peer that its operation is now in this host's fold (FR-009).</summary>
    private async Task SendAckAsync(string peer, Dot opId, CancellationToken ct)
    {
        try { await _link.SendAsync(peer, AckProtocol.Box, AckProtocol.Encode(opId), ct).ConfigureAwait(false); }
        catch { /* advisory: convergence does not depend on it, only the acceptance measurement does */ }
    }

    /// <summary>True once some peer has attested that it folded this operation.</summary>
    public bool WasAckedByPeer(Dot opId)
    {
        lock (_ackGate) return _acked.Contains(opId);
    }

    /// <summary>
    /// Wait until a peer attests to having folded <paramref name="opId"/>, or the timeout expires.
    /// <para>
    /// This is what makes SC-001 a measurement rather than a local timing. Returns false on timeout;
    /// the caller records UNKNOWN, never a green.
    /// </para>
    /// </summary>
    public async Task<bool> WaitForPeerAckAsync(Dot opId, TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = _clock.GetUtcNow() + timeout;
        while (true)
        {
            TaskCompletionSource waiter;
            lock (_ackGate)
            {
                if (_acked.Contains(opId)) return true;
                waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _ackWaiters.Add(waiter);
            }

            var remaining = deadline - _clock.GetUtcNow();
            if (remaining <= TimeSpan.Zero) { lock (_ackGate) _ackWaiters.Remove(waiter); return false; }

            var timer = Task.Delay(remaining, _clock, ct);
            var done = await Task.WhenAny(waiter.Task, timer).ConfigureAwait(false);
            lock (_ackGate) _ackWaiters.Remove(waiter);

            if (done == timer)
            {
                lock (_ackGate) return _acked.Contains(opId);
            }
        }
    }

    /// <summary>This host's own declared capabilities — it IS term-space aware (FR-013..FR-018).</summary>
    public PeerCapabilities LocalCapabilities => new(TermSpaceAware: true, _config.SpaceId);

    /// <summary>Tell a peer what this host supports, so its gate can admit us.</summary>
    /// <param name="isReply">
    /// True when answering a peer's declaration. A reply is never answered again, which terminates
    /// the exchange after exactly one round trip however many times either side restarts.
    /// </param>
    public async Task AnnounceCapabilitiesAsync(string peerName, CancellationToken ct = default,
                                                bool isReply = false)
    {
        try
        {
            await _link.SendAsync(peerName, HelloProtocol.Box,
                HelloProtocol.Encode(LocalCapabilities, isReply), ct).ConfigureAwait(false);
        }
        catch { /* the peer will refuse our pushes until it hears this; fail-closed is correct */ }
    }

    /// <summary>What a peer declared, or null if it has not declared anything. For the status surface.</summary>
    public PeerCapabilities? DeclaredCapabilitiesOf(string peerName) =>
        _peerCaps.TryGetValue(peerName, out var c) ? c : null;

    /// <summary>
    /// Note that an operation crossed, and from whom when that is known.
    /// </summary>
    private void RecordCrossing(string? fromPeer)
    {
        if (fromPeer is null) return;
        var entry = _peers.Find(fromPeer) ?? _peers.Entries.FirstOrDefault(e => e.Name == fromPeer);
        lock (_machineGate) _crossedFrom.Add(entry?.NodeId ?? fromPeer);
    }

    /// <summary>
    /// The same-machine verdict over the peers an operation ACTUALLY crossed from.
    /// <para>
    /// CONSERVATIVE BY CONSTRUCTION: if ANY crossing came from a same-machine peer the answer is
    /// Yes, because one same-machine crossing is enough to disqualify the evidence (FR-022). An
    /// unmeasured or unattributed crossing is Unknown. Only when every crossing is measured and
    /// remote is the answer No.
    /// </para>
    /// </summary>
    /// <summary>True once any frame — an operation or an ack — has crossed from a named peer.</summary>
    private bool HasObservedCrossing
    {
        get { lock (_machineGate) return _crossedFrom.Count > 0; }
    }

    private Tri SameMachineVerdict()
    {
        lock (_machineGate)
        {
            if (_crossedFrom.Count == 0) return Tri.Unknown;   // crossed, but from nobody we can name

            bool anyUnknown = false;
            foreach (var peer in _crossedFrom)
            {
                if (!_sameMachineByPeer.TryGetValue(peer, out var t)) { anyUnknown = true; continue; }
                if (t == Tri.Yes) return Tri.Yes;              // one is enough to disqualify
                if (t == Tri.Unknown) anyUnknown = true;
            }
            return anyUnknown ? Tri.Unknown : Tri.No;
        }
    }

    /// <summary>Record that a peer completed mutual verification, keyed by NODE ID (FR-007).</summary>
    private void MarkAdmitted(string peerName)
    {
        var entry = _peers.Find(peerName) ?? _peers.Entries.FirstOrDefault(e => e.Name == peerName);
        if (entry is null) return;
        _admitted[entry.NodeId] = 0;
        _connected[entry.NodeId] = 0;   // a frame just arrived on it, so the link is live
    }

    /// <summary>
    /// Send this host's frontier to a peer — the REQUEST half of FR-028's pull leg. Frontier first,
    /// never the whole log: with N hosts and M ops the latter is O(N·M) every interval, growing
    /// without bound exactly as the board becomes useful.
    /// </summary>
    public async Task RequestPullAsync(string peerName, CancellationToken ct = default)
    {
        try
        {
            await _link.SendAsync(peerName, PullProtocol.RequestBox,
                PullProtocol.EncodeRequest(_fold.Frontier), ct).ConfigureAwait(false);
        }
        catch
        {
            // A pull that cannot be sent is retried at the next interval. That IS the backstop —
            // it must not throw, or one unreachable peer would stop reconciliation with every peer.
            // But the link is marked DOWN, so the next interval re-dials rather than sending into
            // the same dead connection forever.
            var entry = _peers.Find(peerName) ?? _peers.Entries.FirstOrDefault(e => e.Name == peerName);
            if (entry is not null) _connected.TryRemove(entry.NodeId, out _);
        }
    }

    /// <summary>
    /// Answer a peer's pull with only the ops it lacks — the RESPONSE half, in frame-sized batches.
    /// <para>
    /// A peer far enough behind produces more than one frame's worth of ops. Encoding them into a
    /// single frame exceeds the transport's 64 MiB guard, which REJECTS it — and the identical
    /// oversized frame is then rebuilt and rejected at every interval, so the peer never makes any
    /// progress at all. Batches are sent in dot order, so a peer that receives only the first of
    /// them still converges monotonically.
    /// </para>
    /// </summary>
    public async Task AnswerPullAsync(string peerName, FederationFrontier peerFrontier, CancellationToken ct = default)
    {
        var missing = OpsMissingFrom(peerFrontier);
        if (missing.Count == 0) return;

        foreach (var batch in PullProtocol.BatchResponses(missing))
        {
            try
            {
                await _link.SendAsync(peerName, PullProtocol.ResponseBox,
                    PullProtocol.EncodeResponse(batch), ct).ConfigureAwait(false);
            }
            catch
            {
                // Stop at the first failure: the batches are a prefix sequence, so sending later
                // ones past a gap would hand the peer a set it cannot use. Retried next interval.
                var entry = _peers.Find(peerName) ?? _peers.Entries.FirstOrDefault(e => e.Name == peerName);
                if (entry is not null) _connected.TryRemove(entry.NodeId, out _);
                return;
            }
        }
    }

    /// <summary>
    /// The 60-second reconciliation loop (FR-028). Runs until cancelled. Until this existed the
    /// interval was configured, validated and PRINTED TO THE OPERATOR while nothing read it.
    /// </summary>
    public async Task RunPullLoopAsync(CancellationToken ct)
    {
        var period = TimeSpan.FromSeconds(Math.Max(1, _config.PullIntervalSeconds));
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(period, _clock, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            foreach (var entry in _peers.Entries)
            {
                // RECONNECT FIRST. Without this the loop could only pull from peers whose FIRST
                // dial happened to succeed: a peer missed at startup was skipped forever, and a
                // peer whose established connection later closed stayed "admitted" while every
                // pull went into a connection that no longer existed and failed silently. Restoring
                // the network then repaired nothing until the process was restarted — in a loop
                // whose entire purpose is to be the repair path (FR-028).
                if (!_connected.ContainsKey(entry.NodeId))
                {
                    try { await DialAsync(entry.NodeId, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                    catch { /* still unreachable; try again next interval */ }
                }

                if (!_connected.ContainsKey(entry.NodeId)) continue;
                await RequestPullAsync(entry.NodeId, ct).ConfigureAwait(false);
            }

            PublishStatus();   // keeps the separate `status` command's reading fresh
        }
    }

    /// <summary>
    /// The reconciliation pull (FR-028). Exchanges the causal frontier FIRST and transfers only the
    /// ops the peer lacks — shipping the whole log every 60 s is a broadcast storm, not a backstop.
    /// </summary>
    /// <param name="fromPeer">
    /// The authenticated sender, when known. Carried so a crossing recovered through the pull can be
    /// attributed — without it the same-machine verdict stays Unknown even for a measured peer.
    /// </param>
    public async Task<int> ReconcileAsync(IReadOnlyCollection<FederationOp> peerOps,
                                          PeerCapabilities peer,
                                          CancellationToken ct = default,
                                          string? fromPeer = null)
    {
        // FR-018 ENFORCED HERE, not merely declared. This is the merge, so this is where the gate
        // has to sit: a gate that only exists in a unit test is a green test over an ungated path.
        // Merging under the older ordering rule is the irreversible mistake, so a refusal THROWS
        // rather than returning 0 — a silent no-op would be indistinguishable from "the peer had
        // nothing to send".
        var verdict = MergeGate.CanMerge(peer, localTermSpaceAware: true);
        if (!verdict.Allowed)
            throw new MergeRefusedException(verdict.Reason);

        int added = 0;
        var refusals = new List<AttributionRefusedException>();
        foreach (var op in peerOps)
        {
            // Same admission and same ordering as the push path — attribution checked, durable
            // write, then visible. There is one way into the fold, not two with different rules.
            //
            // A REFUSAL SKIPS ONE OPERATION, NOT THE REST OF THE BATCH. Aborting here stranded
            // every later op behind the refused one: the refused op never enters the frontier, so
            // the peer resends it FIRST at every interval, and nothing after it ever converges. One
            // malformed entry would have been enough to stop reconciliation permanently.
            try
            {
                if (await AdmitAndFoldAsync(op, ct).ConfigureAwait(false))
                {
                    added++;
                    _opCrossed = true;
                    RecordCrossing(fromPeer);        // attributed when the sender is known
                }
            }
            catch (AttributionRefusedException ex)
            {
                RefusedOps++;
                refusals.Add(ex);
            }
        }
        if (added > 0) PublishStatus();

        // Refusals are REPORTED, never swallowed — but after the valid work has been done.
        if (refusals.Count > 0)
            OnRefusal?.Invoke(refusals[0]);

        return added;
    }

    /// <summary>The ops a peer lacks, given its frontier. The reply half of the pull.</summary>
    public IReadOnlyList<FederationOp> OpsMissingFrom(FederationFrontier peerFrontier) =>
        _fold.Operations.Where(o => !peerFrontier.Contains(o.OpId)).ToList();

    /// <summary>
    /// Assemble the status. Every field is set only by its own measurement (FR-020) — there is no
    /// path here in which one probe's result feeds another's.
    /// </summary>
    public FederationStatus Status()
    {
        var reasons = new Dictionary<string, string>();

        var stack = FederationStatusProbe.MeasureStackSupported();

        Tri bound = _policyRefusal is not null ? Tri.No : _bound ? Tri.Yes : Tri.No;
        if (_policyRefusal is not null) reasons["listener bound"] = "blocked by host software policy";

        Tri admitted;
        if (_peers.AdmitsNobody) { admitted = Tri.No; reasons["peer admitted"] = _peers.WhyNotAdmitted(); }
        else if (_admitted.Count > 0) admitted = Tri.Yes;
        else { admitted = Tri.No; reasons["peer admitted"] = _peers.WhyNotAdmitted(); }

        Tri crossed = _opCrossed ? Tri.Yes : Tri.No;

        return new FederationStatus
        {
            StackSupported = stack,
            ListenerBound = bound,
            PeerAdmitted = admitted,
            OpReceivedFromPeer = crossed,
            // A crossing WITHOUT a measured peer address is Unknown, never "n/a". The passive side
            // and the reconciliation-pull path both learn of an op without learning where it came
            // from, and reporting that as "no crossing observed" beside "op received: yes" would be
            // the surface contradicting itself (FR-021).
            // GATED ON ANY OBSERVED CROSSING, not on _opCrossed alone.
            //
            // _opCrossed means "an operation was received FROM a peer" — the fourth reported state.
            // An ACK is not that: it is the peer attesting it folded OURS. But it is still a frame
            // that genuinely crossed, so the same-machine question IS answerable for it, and the
            // acceptance run depends on that answer. Collapsing the two facts left SameMachine null
            // after a successful remote fold.
            SameMachine = (_opCrossed || HasObservedCrossing) ? SameMachineVerdict() : null,
            PolicyRefused = _policyRefusal,
            Reasons = reasons,
            BoundEndpoint = _bound ? _link.ListenEndPoint?.ToString() : null,
            AdmittedParticipants = _admitted.Count,
        };
    }

    /// <summary>
    /// Watch a log file for operations appended by ANOTHER process and push them (FR-028's push leg
    /// across a process boundary).
    /// <para>
    /// `serve` and `post` are separate processes — that is what the runbook instructs. Without this
    /// loop, `post` appended locally and the running daemon never learned of it, so a posted claim
    /// reached no peer until the next pull happened to carry it (and, before the frontier was
    /// hole-preserving, possibly never). The durable log IS the channel: it is already append-only,
    /// already crash-safe and already the thing both processes agree on, so no socket, port or pipe
    /// is introduced to carry what a file already carries.
    /// </para>
    /// <para>
    /// Ops already in the fold are skipped, so a restart re-reading the file pushes nothing twice,
    /// and the loop never appends — it only reads what another process made durable.
    /// </para>
    /// </summary>
    public Task RunLogTailAsync(string path, CancellationToken ct) =>
        RunLogTailAsync(new[] { path }, ct);

    /// <summary>
    /// Watch SEVERAL logs — this host's federation log AND the live lane segments under the board
    /// root.
    /// <para>
    /// Tailing only this host's own federation file meant `serve` replayed every actor's log at
    /// startup and then went blind: a claim a real lane appended a minute later never entered the
    /// running fold and was never pushed until the daemon restarted. The board is the thing that
    /// changes; watching only the part of it this process writes is watching the wrong thing.
    /// </para>
    /// <para>
    /// The set is re-globbed each tick, so a newly rotated segment or a newly active actor is picked
    /// up without a restart.
    /// </para>
    /// </summary>
    public async Task RunBoardTailAsync(string boardRoot, string ownPath, CancellationToken ct)
    {
        var offsets = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var period = TimeSpan.FromSeconds(1);

        // START AT ZERO, NOT AT THE CURRENT LENGTH.
        //
        // Snapshotting the length here opened a silent gap: an operation appended between startup
        // replay finishing and this line running was neither replayed NOR tailed, and could not
        // enter the fold or be advertised until the daemon restarted. Re-reading from the start is
        // free of that race and costs nothing, because the fold deduplicates by dot — everything
        // replay already folded returns false from Apply and is not pushed twice.
        foreach (var p in EnumerateBoardLogs(boardRoot, ownPath)) offsets[p] = 0;

        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(period, _clock, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            foreach (var p in EnumerateBoardLogs(boardRoot, ownPath))
            {
                // A file that appeared since the last tick starts at 0 so its contents are read.
                long from = offsets.TryGetValue(p, out var o) ? o : 0;
                var (lines, next) = ReadCompleteLinesFrom(p, from);
                offsets[p] = next;

                foreach (var text in lines) await FoldAndPushTailedLineAsync(text, ct).ConfigureAwait(false);
            }
        }
    }

    private static IEnumerable<string> EnumerateBoardLogs(string boardRoot, string ownPath)
    {
        var seen = new List<string> { ownPath };
        try
        {
            seen.AddRange(BoardRoot.AllActorLogs(boardRoot));
            string fedDir = Path.Combine(boardRoot, SchedulerBoardLog.FederationKindName);
            if (Directory.Exists(fedDir))
                seen.AddRange(Directory.EnumerateDirectories(fedDir)
                    .SelectMany(d => Directory.EnumerateFiles(d, "*.jsonl")));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return seen.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public async Task RunLogTailAsync(IReadOnlyList<string> paths, CancellationToken ct)
    {
        // START AT ZERO, matching RunBoardTailAsync. Snapshotting the current length opens the same
        // replay-to-tail race: an operation appended between replay finishing and this line running
        // is neither replayed nor tailed. Re-reading costs nothing because the fold deduplicates by
        // dot, and the read is chunk-bounded so a large backlog cannot fault the task.
        var offsets = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths) offsets[path] = 0;

        var period = TimeSpan.FromSeconds(1);
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(period, _clock, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            foreach (var path in paths)
            {
                var (lines, next) = ReadCompleteLinesFrom(path, offsets.TryGetValue(path, out var o) ? o : 0);
                offsets[path] = next;
                foreach (var text in lines) await FoldAndPushTailedLineAsync(text, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Fold one tailed line and push it. Handles BOTH line schemas on the board.
    /// <para>
    /// A real lane appends a SCHEDULER-NATIVE record, whose <c>op_id</c> is a string — so
    /// <c>FederationOp.FromJson</c> throws and an earlier version silently discarded it. The tail
    /// then watched the live `ops` directories and pushed nothing from them, which is the whole
    /// point of watching them. The adapter that startup replay uses has to be on this path too.
    /// </para>
    /// </summary>
    private async Task FoldAndPushTailedLineAsync(string text, CancellationToken ct)
    {
        FederationOp? op = null;
        try { op = FederationOp.FromJson(text); }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            // Not federation-native. Try the scheduler-native shape before giving up on it.
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    op = SchedulerBoardLog.AdaptSchedulerLine(doc.RootElement);
            }
            catch (Exception adapt) when (adapt is JsonException
                                                   or ArgumentOutOfRangeException
                                                   or ArgumentException
                                                   or FormatException
                                                   or InvalidOperationException)
            {
                // ONE BAD LINE, NOT THE WHOLE TAIL. A scheduler row with a nonpositive seq makes the
                // adapter throw ArgumentOutOfRangeException through FederationOp.Create; catching
                // only JsonException let it fault the board-tail task — which nothing awaits — so
                // every later local append silently stopped being folded or pushed.
                op = null;
            }
        }

        if (op is null) return;   // a partial line, or a record of neither schema

        // ADMISSION APPLIES HERE TOO — but WITHOUT re-appending.
        //
        // A tailed record is BY DEFINITION already on disk: it is being read FROM the log. In
        // `serve`, `_log` is the very SchedulerBoardLog whose path is tailed, so routing this
        // through AdmitAndFoldAsync wrote a second copy of every operation `post` created —
        // duplicating each live post in the append-only journal, which nothing can undo.
        //
        // The gate still runs; only the write is skipped, because it already happened.
        try
        {
            if (!await AdmitAlreadyDurableAsync(op, ct).ConfigureAwait(false)) return;
        }
        catch (AttributionRefusedException ex)
        {
            // COUNTED HERE TOO. Replay and reconciliation both increment it, so counting only on
            // those paths made the documented metric depend on which ingestion path happened to
            // encounter the refusal rather than on how many refusals occurred.
            RefusedOps++;
            OnRefusal?.Invoke(ex);
            return;
        }
        catch (DotConflictException) { return; }   // a conflict is refused, never pushed onward

        await PushAsync(op, ct).ConfigureAwait(false);
        PublishStatus();   // the fold changed; a surface that does not say so is stale
    }

    /// <summary>
    /// Fold the operations replayed from disk at startup, applying the SAME admission checks the
    /// live paths apply.
    /// <para>
    /// Startup used to insert replayed ops straight into the fold, so <c>require_verified_attribution</c>
    /// was bypassed after every restart: an unsigned or tampered operation already on the board became
    /// visible and eligible for propagation. A gate that a restart turns off is not a gate.
    /// </para>
    /// <para>
    /// Refused operations are COUNTED and reported, not dropped silently, and they are never removed
    /// from the log — FR-011 holds even for an operation this host will not fold.
    /// </para>
    /// </summary>
    public int ReplayIntoFold(IEnumerable<FederationOp> ops)
    {
        int folded = 0;
        foreach (var op in ops)
        {
            var attribution = OpAttribution.Check(op, KeyForOrigin(op.Origin));

            if (!attribution.Acceptable(RequireVerifiedAttribution))
            {
                RefusedOps++;
                OnRefusal?.Invoke(new AttributionRefusedException(attribution));
                continue;
            }
            if (attribution.Verdict == AttributionVerdict.UnverifiedOrigin) UnverifiedOps++;

            try { if (_fold.Apply(op)) folded++; }
            catch (DotConflictException) { RefusedOps++; }
        }
        return folded;
    }

    /// <summary>
    /// Read only COMPLETE lines from <paramref name="from"/>, returning the new offset. A trailing
    /// unterminated fragment is left unread so the next poll sees the whole record.
    /// </summary>
    private static (IReadOnlyList<string> Lines, long Next) ReadCompleteLinesFrom(string path, long from)
    {
        var lines = new List<string>();
        try
        {
            if (!File.Exists(path)) return (lines, from);
            long length = new FileInfo(path).Length;

            // A shorter file means it was replaced, not appended to. Re-read from the start rather
            // than seeking past the end of the new content and going permanently blind.
            if (length < from) from = 0;
            if (length == from) return (lines, from);

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            fs.Seek(from, SeekOrigin.Begin);

            // BOUNDED READ. Starting every log at offset 0 (which is what closes the replay-to-tail
            // race) means "the remaining suffix" is the WHOLE FILE on the first pass. Allocating
            // that in one buffer OOMs or overflows on a large board, faulting the tail task — and
            // nothing awaits that task, so push-on-append would stop permanently and silently.
            // Reading a bounded chunk per tick converges just as surely, one chunk at a time.
            int want = (int)Math.Min(MaxTailChunkBytes, length - from);
            var buffer = new byte[want];
            int read = fs.Read(buffer, 0, buffer.Length);
            string text = Encoding.UTF8.GetString(buffer, 0, read);

            int lastBreak = text.LastIndexOf('\n');
            if (lastBreak < 0)
            {
                // NO NEWLINE IN A FULL CHUNK IS NOT "nothing complete yet" — it is a record longer
                // than the chunk. Returning the original offset re-read the same bytes forever, so
                // that record and EVERY LATER RECORD in the file stopped converging. Grow the read
                // until a line ends or the record-size ceiling is hit.
                // ONLY a FULL chunk with no newline means "the record is bigger than the chunk".
                // A short read is just a writer mid-append — the normal case — and skipping that
                // would discard exactly the operation the partial-line fix exists to preserve.
                if (want < MaxTailChunkBytes || read < want) return (lines, from);
                if ((long)want >= MaxTailRecordBytes) return (lines, from);

                int grown = (int)Math.Min(MaxTailRecordBytes, length - from);
                fs.Seek(from, SeekOrigin.Begin);
                var big = new byte[grown];
                int bigRead = fs.Read(big, 0, big.Length);
                text = Encoding.UTF8.GetString(big, 0, bigRead);
                lastBreak = text.LastIndexOf('\n');

                if (lastBreak < 0)
                {
                    // ONLY skip when the CEILING is genuinely reached. Advancing whenever the grown
                    // read found no newline discarded the prefix of a perfectly ordinary large
                    // record whose writer had simply not appended its newline yet — the remainder was
                    // then parsed without its beginning, losing the operation and every later one.
                    if ((long)bigRead >= MaxTailRecordBytes) return (lines, from + bigRead);

                    return (lines, from);   // still being written; wait for the newline
                }
            }

            foreach (var line in text[..lastBreak].Split('\n'))
                if (!string.IsNullOrWhiteSpace(line)) lines.Add(line.TrimEnd('\r'));

            return (lines, from + Encoding.UTF8.GetByteCount(text[..(lastBreak + 1)]));
        }
        catch (IOException) { return (lines, from); }
        catch (UnauthorizedAccessException) { return (lines, from); }
    }


    /// <summary>
    /// Refresh the published measurement on its own schedule.
    /// <para>
    /// WHY THIS IS SEPARATE FROM THE PULL LOOP. Publishing only on pull ticks tied the heartbeat's
    /// refresh rate to <c>pull_interval_seconds</c> — 60 s by default, against a 30 s freshness
    /// window. A perfectly healthy daemon therefore spent half of every minute looking DEAD to
    /// `status`, which would have read as "listener bound: unknown" and sent an operator hunting a
    /// fault that was not there. Measured live on GAVRIELLA before this loop existed: "measured 23s
    /// ago" and climbing between ticks.
    /// </para>
    /// </summary>
    public async Task RunStatusHeartbeatAsync(CancellationToken ct)
    {
        if (StatusHeartbeatPath is null) return;

        // Comfortably inside the freshness window, so a reader never sees a healthy daemon expire.
        var period = TimeSpan.FromSeconds(StatusHeartbeat.Freshness.TotalSeconds / 3);
        while (!ct.IsCancellationRequested)
        {
            PublishStatus();
            try { await Task.Delay(period, _clock, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>
    /// Publish the measured status so a SEPARATE `status` process can read it (FR-019/FR-021).
    /// <para>
    /// Fail-safe: a status file that cannot be written must never stop the daemon federating. The
    /// reader's staleness window then makes the absent measurement report as unknown, which is the
    /// honest answer, rather than as a negative.
    /// </para>
    /// </summary>
    public void PublishStatus()
    {
        if (StatusHeartbeatPath is null) return;
        try { StatusHeartbeat.From(Status(), _fold.Count, _clock.GetUtcNow()).Publish(StatusHeartbeatPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _pumpCts?.Cancel();
        _pumpCts?.Dispose();
        await _link.DisposeAsync().ConfigureAwait(false);
    }
}
