// S4 policy service — the network-callable mint/attenuate endpoint (feature 058 T035,
// contract s4-policy-service.md §A, FR-008; E-5 ruling: minting lives at GLPNET, yngenios stays
// verify-side only).
//
// Exposed at the pre-cut seam (CapabilitySlot 0x20 + ICapabilityGate): mint stops being
// in-process static-root-key ONLY — this endpoint is the network-callable locus. The in-process
// path (StaticMacaroonMaterial + Macaroon.Create / MacaroonV2.Mint) stays available INTERNALLY;
// remote callers go through this service and NEVER see the root key: requests carry
// identifier/location/caveats, replies carry only finished tokens (an HMAC tail cannot be
// inverted to the key, and attenuation extends the chain without it).
//
// Fail-closed discipline (FR-008/FR-009 posture): every defective request — malformed envelope,
// unknown op, out-of-vocabulary caveat, or an attenuate presenting a token that fails the HMAC
// chain against this service's root key — is answered with a DISTINCT loud Refused reply, never
// a silent drop and never a crash of the serve loop.
//
// ── Recorded follow-ups (OUT OF SCOPE this cycle, per contract s4-policy-service.md §A) ──
//   * Revocation: no revocation list / epoch check exists yet — a minted token stays valid to
//     its caveats' limits until cutover semantics land.
//   * Third-party caveats: only first-party C3 caveats are supported; the third-party
//     (discharge-macaroon) mechanism is a named follow-up, not an acceptance condition.

using GlpRuntime.CrdtMsg.Cap;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Headers;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.CrdtMsg.Route;

namespace GlpRuntime.CrdtMsg.Policy;

/// <summary>The S4 policy-service endpoint: holds the macaroon root key and serves
/// <see cref="PolicyRequest.Mint"/> / <see cref="PolicyRequest.Attenuate"/> over an
/// <see cref="ILinkTransport"/> (the 050 seam — in-memory fabric or the QUIC adapter, unchanged).</summary>
public sealed class PolicyService
{
    /// <summary>The section types this endpoint understands (must-understand policy, FR-005).</summary>
    private static readonly IReadOnlySet<long> Understood =
        new HashSet<long> { PolicyWire.RequestSectionType, CapabilitySlot.SectionType };

    private readonly ILinkTransport _link;
    private readonly byte[] _rootKey;
    private long _seq;

    /// <summary>This service's authenticated peer-name on the transport.</summary>
    public string Peer => _link.LocalPeer;

    /// <param name="link">The transport seam endpoint this service answers on.</param>
    /// <param name="rootKey">The macaroon root secret. Held HERE only — never serialized into any
    /// reply (callers receive tokens, not keys).</param>
    public PolicyService(ILinkTransport link, byte[] rootKey)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(rootKey);
        if (rootKey.Length == 0)
            throw new CrdtMsgException("policy-service root key must be non-empty — fail closed");
        _link = link;
        _rootKey = rootKey;
    }

    /// <summary>Serve exactly <paramref name="count"/> inbound requests (the MeshNode.PumpAsync
    /// idiom — deterministic for tests; a deployment loops it).</summary>
    public async Task ServeAsync(int count, CancellationToken ct = default)
    {
        for (int i = 0; i < count; i++)
        {
            var inbound = await _link.Inbound.ReadAsync(ct);
            await HandleAsync(inbound, ct);
        }
    }

    private async Task HandleAsync(LinkInbound inbound, CancellationToken ct)
    {
        string msgId = "policy#malformed";
        byte[] replyBody;
        try
        {
            Message msg = MessageCodec.Binary.Decode(inbound.Bytes);
            DecodeGuard.CheckMustUnderstand(msg, Understood);
            msgId = msg.Header.MsgId;   // the reply echoes the request's msg_id (correlation)

            byte[] requestBytes = msg.Sections.FirstOrDefault(s => s.TypeNumber == PolicyWire.RequestSectionType)?.Value
                ?? throw new CrdtMsgException("message carries no policy request section — fail closed");
            replyBody = PolicyWire.EncodeOkReply(Execute(PolicyWire.DecodeRequest(requestBytes)));
        }
        catch (CrdtMsgException ex)
        {
            // A refusal is a DISTINCT, loud outcome — the caller gets the diagnostic, the serve
            // loop stays graceful (never a crash, never a silent drop).
            replyBody = PolicyWire.EncodeRefusedReply(ex.Message);
        }

        Message reply = PolicyWire.Envelope(
            msgId, Peer, inbound.FromPeer, _seq++, PolicyWire.ReplySectionType, replyBody);
        await _link.SendAsync(inbound.FromPeer, MessageCodec.Binary.Encode(reply), ct);
    }

    private byte[] Execute(PolicyRequest request) => request switch
    {
        PolicyRequest.Mint m =>
            MacaroonV2Codec.Encode(MacaroonV2.Mint(_rootKey, m.Location, m.Identifier, m.Caveats)),
        PolicyRequest.Attenuate a => ExecuteAttenuate(a),
        _ => throw new CrdtMsgException($"unhandled policy request kind {request.GetType().Name}"),
    };

    private byte[] ExecuteAttenuate(PolicyRequest.Attenuate a)
    {
        MacaroonV2 token = MacaroonV2Codec.Decode(a.Token);
        // Fail-closed: never extend a chain this service's root key did not produce — a forged or
        // tampered token is refused HERE, not laundered into a plausible-looking attenuation.
        if (!token.VerifySignature(_rootKey))
            throw new CrdtMsgException(
                "attenuate refused: presented token fails the HMAC chain against this service's root key "
                + "(forged/tampered) — fail closed");
        return MacaroonV2Codec.Encode(token.Attenuate(a.Caveat));
    }
}
