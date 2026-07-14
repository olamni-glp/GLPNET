// S4 policy-service client face (feature 058 T035): mint/attenuate against a remote
// PolicyService over the ILinkTransport seam. The client never holds or sees the root key —
// it sends identifier/location/caveats and receives finished macaroon-v2 tokens.

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Headers;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.CrdtMsg.Route;

namespace GlpRuntime.CrdtMsg.Policy;

/// <summary>Thrown when the policy service answers with a fail-closed
/// <see cref="PolicyReply.Refused"/> — the diagnostic is the service's loud reason.</summary>
public sealed class PolicyRefusedException : Exception
{
    public PolicyRefusedException(string message) : base(message) { }
}

/// <summary>Caller-side face of the S4 policy endpoint (one in-flight request at a time —
/// request/reply correlation is by echoed msg_id).</summary>
public sealed class PolicyClient
{
    private static readonly IReadOnlySet<long> Understood =
        new HashSet<long> { PolicyWire.ReplySectionType, CapabilitySlot.SectionType };

    private readonly ILinkTransport _link;
    private readonly string _policyPeer;
    private long _seq;
    private int _msgCounter;

    /// <param name="link">This caller's transport seam endpoint.</param>
    /// <param name="policyPeer">The policy service's peer-name on the mesh.</param>
    public PolicyClient(ILinkTransport link, string policyPeer)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyPeer);
        _link = link;
        _policyPeer = policyPeer;
    }

    /// <summary>Request a freshly-minted macaroon-v2 token. Returns the token's wire bytes
    /// (<c>MacaroonV2Codec</c> form). Throws <see cref="PolicyRefusedException"/> on refusal.</summary>
    public Task<byte[]> MintAsync(
        string identifier, string location, IReadOnlyList<string> caveats, CancellationToken ct = default) =>
        RoundTripAsync(PolicyWire.EncodeMintRequest(identifier, location, caveats), ct);

    /// <summary>Request attenuation of <paramref name="token"/> by one additional C3 caveat.
    /// Returns the narrower token's wire bytes. Throws <see cref="PolicyRefusedException"/> on refusal.</summary>
    public Task<byte[]> AttenuateAsync(byte[] token, string caveat, CancellationToken ct = default) =>
        RoundTripAsync(PolicyWire.EncodeAttenuateRequest(token, caveat), ct);

    private async Task<byte[]> RoundTripAsync(byte[] requestBody, CancellationToken ct)
    {
        // deterministic, unique-per-caller msg_id (the MeshNode idiom — peer + monotonic counter)
        string msgId = $"{_link.LocalPeer}#pol{_msgCounter++}";
        Message request = PolicyWire.Envelope(
            msgId, _link.LocalPeer, _policyPeer, _seq++, PolicyWire.RequestSectionType, requestBody);
        await _link.SendAsync(_policyPeer, MessageCodec.Binary.Encode(request), ct);

        LinkInbound inbound = await _link.Inbound.ReadAsync(ct);
        Message reply = MessageCodec.Binary.Decode(inbound.Bytes);
        DecodeGuard.CheckMustUnderstand(reply, Understood);
        if (reply.Header.MsgId != msgId)
            throw new CrdtMsgException(
                $"policy reply correlation mismatch: expected msg_id '{msgId}', got '{reply.Header.MsgId}'");

        byte[] replyBytes = reply.Sections.FirstOrDefault(s => s.TypeNumber == PolicyWire.ReplySectionType)?.Value
            ?? throw new CrdtMsgException("policy reply carries no reply section — fail closed");

        return PolicyWire.DecodeReply(replyBytes) switch
        {
            PolicyReply.Ok ok => ok.Token,
            PolicyReply.Refused refused => throw new PolicyRefusedException(refused.Reason),
            var other => throw new CrdtMsgException($"unhandled policy reply kind {other.GetType().Name}"),
        };
    }
}
