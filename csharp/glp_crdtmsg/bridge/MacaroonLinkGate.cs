// The "quic" link's macaroon capability gate — verify-before-act (feature 050 US3, T026/T027).
//
// Contract capability-gating.md (FR-008/FR-009): establishment and gated actions present a STATIC
// macaroon (beacon static-macaroon model) and Macaroon.Verify() it BEFORE acting. This is the one
// concrete ICapabilityGate: it lives in glp_crdtmsg (which already references glp_link) and is
// injected at the composition root for LinkScheme.Quic, mirroring the CrdtMsgPayloadCodec seam shape
// (research D-1) — glp_link stays capability-agnostic, no reference cycle (G4: host-side only, no
// GLP guard/kernel/primitive).
//
// Outcomes are recorded 100% (041 Provenance contract C19): every gated action — establishment or
// inbound delivery, allowed or refused — appends exactly one ProvenanceRecord; a refusal is the
// DISTINCT ProvenanceOutcome.Refused, never a silent drop (G2). The gate layers ABOVE the mutual-pin
// QUIC handshake: the pin authenticates the peer, the macaroon authorizes the action (G3).

using System.Security.Cryptography;

using GlpRuntime.CrdtMsg.Cap;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Headers;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.Link.Seam;

namespace GlpRuntime.CrdtMsg.Bridge;

/// <summary>Macaroon-backed <see cref="ICapabilityGate"/> + inbound envelope verifier for the
/// <c>"quic"</c> link. Holds this endpoint's presented static macaroon (attached to every outbound
/// envelope's capability slot) and the shared root key it verifies inbound slots against.</summary>
public sealed class MacaroonLinkGate : ICapabilityGate
{
    /// <summary>Context value for the establishment gate (FR-008).</summary>
    public const string ActionEstablish = "establish";

    /// <summary>Context value for a gated action on an established link — inbound delivery (FR-009).</summary>
    public const string ActionDeliver = "deliver";

    /// <summary>The first-party caveat keys this gate understands (fail-closed on any other, 041 C14):
    /// <c>action</c> (establish/deliver), <c>peer</c> (the acting peer), <c>expires</c> (numeric
    /// clock caveat, the 041 CapabilityTests idiom).</summary>
    private static readonly IReadOnlySet<string> UnderstoodCaveats =
        new HashSet<string> { "action", "peer", "expires" };

    private readonly byte[] _rootKey;
    private readonly Macaroon? _presented;
    private readonly byte[]? _presentedBytes;
    private readonly Func<long> _nowUnixSeconds;

    /// <summary>The append-only outcome log — every gated action records exactly one row (SC-006).</summary>
    public ProvenanceLog Provenance { get; } = new();

    /// <param name="rootKey">The shared static-macaroon root secret (out-of-band, T028).</param>
    /// <param name="presented">This endpoint's static macaroon; <c>null</c> = no capability held —
    /// every gated action fails closed (the ABSENT case).</param>
    /// <param name="nowUnixSeconds">Injectable clock for the <c>expires</c> caveat context
    /// (deterministic tests); defaults to the UTC unix-seconds wall clock.</param>
    public MacaroonLinkGate(byte[] rootKey, Macaroon? presented, Func<long>? nowUnixSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(rootKey);
        if (rootKey.Length == 0)
            throw new ArgumentException("macaroon root key must be non-empty (fail-closed)", nameof(rootKey));
        _rootKey = rootKey;
        _presented = presented;
        _presentedBytes = presented is null ? null : MacaroonCodec.Encode(presented);
        _nowUnixSeconds = nowUnixSeconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    /// <summary>The encoded capability the payload codec attaches to every outbound envelope's slot
    /// (section <c>0x20</c>); <c>null</c> when no macaroon is held.</summary>
    public byte[]? PresentedCapability => _presentedBytes;

    /// <inheritdoc/>
    public bool GateEstablish(LinkId id)
    {
        string target = id.ToString() ?? "link";
        if (_presented is null)
        {
            RecordRefused("local", target, null);
            return false;
        }
        bool ok = _presented.Verify(_rootKey, Context(ActionEstablish, "local"), UnderstoodCaveats);
        Record("local", target, _presentedBytes, ok);
        return ok;
    }

    /// <summary>
    /// Verify-before-act on an inbound gated action (FR-009, T027): extract the envelope's capability
    /// slot, verify it against the root key, RECORD the outcome, and — only when allowed — return the
    /// message with the slot stripped (the capability is codec-owned carriage, never term-visible).
    /// Absent / malformed / tampered / expired / unsatisfiable / un-understood ⇒ record
    /// <see cref="ProvenanceOutcome.Refused"/> and throw <see cref="CapabilityRefusedException"/>;
    /// the pump refuses just this action and the run stays graceful.
    /// </summary>
    public Message VerifyInbound(Message m)
    {
        ArgumentNullException.ThrowIfNull(m);
        string peer = m.Header.From;
        string target = m.Header.To;

        byte[]? slot = CapabilitySlot.Extract(m);
        if (slot is null)
        {
            RecordRefused(peer, target, null);
            throw new CapabilityRefusedException(
                $"inbound gated action from '{peer}' presents no capability slot — fail closed (FR-009)");
        }

        Macaroon carried;
        try
        {
            carried = MacaroonCodec.Decode(slot);
        }
        catch (CrdtMsgException ex)
        {
            RecordRefused(peer, target, slot);
            throw new CapabilityRefusedException(
                $"inbound capability from '{peer}' is malformed — fail closed (FR-009): {ex.Message}");
        }

        if (!carried.Verify(_rootKey, Context(ActionDeliver, peer), UnderstoodCaveats))
        {
            RecordRefused(peer, target, slot);
            throw new CapabilityRefusedException(
                $"inbound capability from '{peer}' failed verification (tampered/expired/unsatisfiable/"
                + "un-understood) — fail closed (FR-009)");
        }

        Record(peer, target, slot, ok: true);
        return m with
        {
            Sections = m.Sections.Where(s => s.TypeNumber != CapabilitySlot.SectionType).ToList(),
        };
    }

    private Dictionary<string, string> Context(string action, string peer) => new()
    {
        ["action"] = action,
        ["peer"] = peer,
        ["expires"] = _nowUnixSeconds().ToString(),
    };

    private void RecordRefused(string peer, string target, byte[]? slot) =>
        Record(peer, target, slot, ok: false);

    private void Record(string peer, string target, byte[]? slot, bool ok) =>
        Provenance.Record(
            peer, target,
            DateTimeOffset.FromUnixTimeSeconds(_nowUnixSeconds()),
            slot is null ? "absent" : Convert.ToHexString(SHA256.HashData(slot)),
            ok ? ProvenanceOutcome.Applied : ProvenanceOutcome.Refused);
}
