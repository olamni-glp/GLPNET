// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Admission (feature 102, T009/T027).
//
// Contract federation-wire.md W2 / data-model I-20..I-23 / FR-005, FR-006, FR-007, FR-008.
//
// KEYED BY NODE ID, NEVER BY ADDRESS. Two of the four hosts on this estate answer on more than one
// address — Olamnit on 192.168.0.136 AND .129 — so any admission decision or participant count
// keyed on address over-counts. Adding an address does not add a participant (SC-006).
//
// AN EMPTY PEER SET ADMITS NOBODY. That is the default and the state the system fails INTO, which
// is why "a reachable listener is not an open one" holds before any of this is configured.

using System.Net;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>
/// Why a dialer was not admitted. THREE distinct conditions, never one generic error — a pin
/// mismatch and an unreachable host demand opposite operator responses (FR-008).
/// </summary>
public enum AdmissionOutcome
{
    Admitted,

    /// <summary>The presented identity is not in the peer set at all (FR-006).</summary>
    NotInPeerSet,

    /// <summary>The identity is known but does not match its recorded pin — investigate (FR-008).</summary>
    PinMismatch,

    /// <summary>The peer could not be reached — wait, or check its firewall. Not a security event.</summary>
    Unreachable,

    /// <summary>The name did not resolve. NOT a transport failure — see contract W1.</summary>
    NameResolutionFailed,
}

/// <summary>One admitted participant: an identity, its pin, and the addresses it answers on.</summary>
public sealed record PeerEntry(string Name, string NodeId, IReadOnlyList<IPEndPoint> Endpoints, string Pin);

/// <summary>The participants this host will admit. Empty by default, meaning admit nobody.</summary>
public sealed class PeerSet
{
    private readonly Dictionary<string, PeerEntry> _byNodeId;

    /// <summary>The safe default: admits nobody (FR-006 / SC-004).</summary>
    public PeerSet() => _byNodeId = new Dictionary<string, PeerEntry>(StringComparer.OrdinalIgnoreCase);

    public PeerSet(IEnumerable<PeerEntry> entries) : this()
    {
        foreach (var e in entries) Add(e);
    }

    /// <summary>
    /// Number of PARTICIPANTS — distinct node ids, not distinct addresses (SC-006).
    /// </summary>
    public int ParticipantCount => _byNodeId.Count;

    /// <summary>True when nobody can be admitted. The honest reason for a "peer admitted: no".</summary>
    public bool AdmitsNobody => _byNodeId.Count == 0;

    public IReadOnlyCollection<PeerEntry> Entries => _byNodeId.Values;

    /// <summary>
    /// Add or replace a participant. A duplicate node id REPLACES rather than adds — one
    /// participant, one entry (contract G3) — while an address already used by another node id is
    /// permitted, because addresses are not identity (I-21).
    /// </summary>
    public void Add(PeerEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.NodeId))
            throw new ArgumentException("a peer entry must carry a node id — identity is not derived from address (FR-007)", nameof(entry));
        _byNodeId[entry.NodeId] = entry;
    }

    public PeerEntry? Find(string nodeId) =>
        _byNodeId.TryGetValue(nodeId, out var e) ? e : null;

    /// <summary>
    /// Decide admission for a presented identity and pin. Called BEFORE any board data is exchanged
    /// (FR-005) — a check performed after the first frame has crossed is not admission control.
    /// </summary>
    public AdmissionOutcome Admit(string presentedNodeId, string presentedPin)
    {
        var entry = Find(presentedNodeId);
        if (entry is null) return AdmissionOutcome.NotInPeerSet;
        return string.Equals(entry.Pin, presentedPin, StringComparison.OrdinalIgnoreCase)
            ? AdmissionOutcome.Admitted
            : AdmissionOutcome.PinMismatch;
    }

    /// <summary>The pin table in the shape <c>QuicLinkTransport</c> expects: peer name -> SPKI pin.</summary>
    public IReadOnlyDictionary<string, string> ToPinTable() =>
        _byNodeId.Values.ToDictionary(e => e.Name, e => e.Pin, StringComparer.Ordinal);

    /// <summary>Human reason for a not-admitted state, naming the missing pins (FR-019 / contract S6).</summary>
    public string WhyNotAdmitted() =>
        AdmitsNobody ? "peer set is empty - no pins configured" : "no peer has completed mutual verification yet";
}
