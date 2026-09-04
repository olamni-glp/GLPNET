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

    private readonly FederationConfig _config;
    private readonly IFederationLink _link;
    private readonly FederationFold _fold;
    private readonly PeerSet _peers;
    private readonly IBoardLog _log;
    private readonly HashSet<string> _admitted = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    private bool _bound;
    private bool _opCrossed;
    private Tri? _sameMachine;   // null until a crossing is observed; see FederationStatus.SameMachine
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
        _clock = clock ?? TimeProvider.System;
    }

    public FederationFold Fold => _fold;
    public PeerSet Peers => _peers;

    /// <summary>Health, derived from what was MEASURED — never from configuration alone.</summary>
    public FederationHealth Health =>
        !_config.Enabled ? FederationHealth.Disabled
        : _admitted.Count > 0 ? FederationHealth.Federating
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
            return true;
        }
        catch (Exception ex) when (PolicyRefusal.Detect(ex) is { } refusal)
        {
            // FR-023: a host-policy refusal is its OWN named failure. It presents as a healthy build
            // and a passing suite followed by a daemon that never runs, so a generic error here
            // costs hours every time.
            _policyRefusal = refusal;
            _bound = false;
            return false;
        }
    }

    /// <summary>
    /// Dial a configured peer by LITERAL address (FR-003). A name that does not resolve is reported
    /// as <see cref="AdmissionOutcome.NameResolutionFailed"/> and NEVER as a transport failure —
    /// every host on this estate resolves to fe80:: link-local only.
    /// </summary>
    public async Task<AdmissionOutcome> DialAsync(string peerName, CancellationToken ct = default)
    {
        var entry = _peers.Entries.FirstOrDefault(e => e.Name == peerName);
        if (entry is null) return AdmissionOutcome.NotInPeerSet;
        if (entry.Endpoints.Count == 0) return AdmissionOutcome.NameResolutionFailed;

        // A participant may answer on several addresses; try each. Reaching it on the second does
        // not make it a second participant (SC-006).
        foreach (var ep in entry.Endpoints)
        {
            try
            {
                await _link.ConnectPeerAsync(peerName, ep, ct).ConfigureAwait(false);
                _admitted.Add(entry.NodeId);
                _sameMachine = FederationStatusProbe.IsSameMachine(
                    _link.ListenEndPoint?.Address ?? IPAddress.Any, ep.Address) ? Tri.Yes : Tri.No;
                return AdmissionOutcome.Admitted;
            }
            catch (System.Security.Authentication.AuthenticationException)
            {
                // FR-008: a pin mismatch and an unreachable host demand OPPOSITE responses, so they
                // are never folded into one generic error.
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
    /// Append an operation and then ship it. THE ORDER IS THE POINT (FR-030): the local durable
    /// write happens first, so an op survives a crash between the two steps and is recovered by the
    /// pull backstop.
    /// </summary>
    public async Task AppendAndPushAsync(FederationOp op, CancellationToken ct = default)
    {
        _fold.Apply(op);
        await _log.AppendAsync(op, ct).ConfigureAwait(false);   // 1. durable

        if (!_config.Enabled || !_config.PushOnAppend) return;
        await PushAsync(op, ct).ConfigureAwait(false);           // 2. best effort
    }

    /// <summary>Push one op to every admitted peer. Best effort — a failure here is repaired by the pull.</summary>
    public async Task PushAsync(FederationOp op, CancellationToken ct = default)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(op.ToCanonicalJson());
        foreach (var entry in _peers.Entries)
        {
            if (!_admitted.Contains(entry.NodeId)) continue;
            try { await _link.SendAsync(entry.Name, BoardBox, bytes, ct).ConfigureAwait(false); }
            catch { /* the 60 s reconciliation pull is the repair path (FR-028) */ }
        }
    }

    /// <summary>
    /// Receive one inbound frame and fold it. Returns true if the op was NEW; false on redelivery.
    /// Redelivery is certain on a retrying link and must never double-count (FR-010).
    /// </summary>
    public async Task<bool> ReceiveOneAsync(CancellationToken ct = default)
    {
        var inbound = await _link.Inbound.ReadAsync(ct).ConfigureAwait(false);
        if (inbound.Box != BoardBox) return false;

        var op = FederationOp.FromJson(Encoding.UTF8.GetString(inbound.Bytes));
        bool isNew = _fold.Apply(op);
        if (isNew) await _log.AppendAsync(op, ct).ConfigureAwait(false);

        _opCrossed = true;
        // The passive side learns of an op without learning where it came from. That is UNKNOWN,
        // not "no crossing observed" — and never silently No (FR-021/FR-022).
        _sameMachine ??= Tri.Unknown;
        return isNew;
    }

    /// <summary>
    /// The reconciliation pull (FR-028). Exchanges the causal frontier FIRST and transfers only the
    /// ops the peer lacks — shipping the whole log every 60 s is a broadcast storm, not a backstop.
    /// </summary>
    public async Task<int> ReconcileAsync(IReadOnlyCollection<FederationOp> peerOps,
                                          PeerCapabilities peer,
                                          CancellationToken ct = default)
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
        foreach (var op in peerOps)
        {
            if (_fold.Contains(op.OpId)) continue;   // already have it — the frontier test
            if (_fold.Apply(op))
            {
                await _log.AppendAsync(op, ct).ConfigureAwait(false);
                added++;
                _opCrossed = true;
                _sameMachine ??= Tri.Unknown;        // an op crossed, but the pull carries no address
            }
        }
        return added;
    }

    /// <summary>The ops a peer lacks, given its frontier. The reply half of the pull.</summary>
    public IReadOnlyList<FederationOp> OpsMissingFrom(VersionVector peerFrontier) =>
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
            SameMachine = _opCrossed ? (_sameMachine ?? Tri.Unknown) : null,
            PolicyRefused = _policyRefusal,
            Reasons = reasons,
            BoundEndpoint = _bound ? _link.ListenEndPoint?.ToString() : null,
            AdmittedParticipants = _admitted.Count,
        };
    }

    public async ValueTask DisposeAsync()
    {
        _pumpCts?.Cancel();
        _pumpCts?.Dispose();
        await _link.DisposeAsync().ConfigureAwait(false);
    }
}
