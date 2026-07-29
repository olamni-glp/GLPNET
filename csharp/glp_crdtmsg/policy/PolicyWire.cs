// S4 policy-service wire surface (feature 058 T035, contract s4-policy-service.md §A).
//
// The mint/attenuate endpoint rides the EXISTING 050 link plumbing — no new transport, no new
// payload type. Following the E3PC precedent (route/E3pcCtrl.cs, 057 T034): one request and one
// reply frame kind, each a reserved MUST-UNDERSTAND (odd) TLV section riding a
// PayloadType.CrdtMessage envelope with CrdtModel.None ("an ordinary request/response, which
// travels the pipe unimpeded"), encoded on the canonical binary surface and carried over the
// ILinkTransport seam (in-memory fabric in tests; the QUIC adapter is the drop-in replacement).
// NOTE a NEW static payload-type byte was deliberately NOT allocated: glp_schema_lang's dynamic
// lowering owns bytes 0x13+ over the 3-entry seed registry (its tests pin "first free byte 0x13"),
// and the repo's convention for new protocol kinds is a reserved section, not a registry row.
//
// Bodies use the shipped ByteWriter/ByteReader conventions (LEB128 varints, varint-length-prefixed
// UTF-8 strings); decode is consume-all-or-throw (loud-fail, FR-005 discipline).

using GlpRuntime.CrdtMsg.Cap;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.ResultCodec;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.CrdtMsg.Policy;

/// <summary>One decoded policy-service request (the two operations of the endpoint).</summary>
public abstract record PolicyRequest
{
    /// <summary>Mint: identifier + location + ordered C3 caveats → a macaroon-v2 token.</summary>
    public sealed record Mint(string Identifier, string Location, IReadOnlyList<string> Caveats) : PolicyRequest;

    /// <summary>Attenuate: an existing token + ONE additional C3 caveat → a narrower token.</summary>
    public sealed record Attenuate(byte[] Token, string Caveat) : PolicyRequest;
}

/// <summary>One decoded policy-service reply.</summary>
public abstract record PolicyReply
{
    /// <summary>Success: the minted/attenuated macaroon-v2 token bytes (MacaroonV2Codec wire form).</summary>
    public sealed record Ok(byte[] Token) : PolicyReply;

    /// <summary>Fail-closed refusal with a loud diagnostic (never a silent drop).</summary>
    public sealed record Refused(string Reason) : PolicyReply;
}

/// <summary>Section framing + body codecs for the policy endpoint.</summary>
public static class PolicyWire
{
    /// <summary>Reserved section type for a policy request. Odd ⇒ must-understand: a receiver that
    /// does not speak the policy protocol fails LOUD rather than silently dropping a capability op.</summary>
    public const long RequestSectionType = 0x17;

    /// <summary>Reserved section type for a policy reply. Odd ⇒ must-understand.</summary>
    public const long ReplySectionType = 0x19;

    // request op discriminants
    public const byte OpMint = 0x01;
    public const byte OpAttenuate = 0x02;

    // reply status discriminants
    public const byte StatusOk = 0x00;
    public const byte StatusRefused = 0x01;

    // ---------------------------------------------------------------- request bodies

    public static byte[] EncodeMintRequest(string identifier, string location, IReadOnlyList<string> caveats)
    {
        var w = new ByteWriter();
        w.WriteByte(OpMint);
        w.WriteString(identifier);
        w.WriteString(location);
        VarInt.WriteU64(w, (ulong)caveats.Count);
        foreach (var caveat in caveats)
            w.WriteString(caveat);
        return w.TakeBytes();
    }

    public static byte[] EncodeAttenuateRequest(byte[] token, string caveat)
    {
        var w = new ByteWriter();
        w.WriteByte(OpAttenuate);
        VarInt.WriteU64(w, (ulong)token.Length);
        w.WriteBytes(token);
        w.WriteString(caveat);
        return w.TakeBytes();
    }

    /// <summary>Decode a request body; consume-all-or-throw (an unknown op byte is a loud fail).</summary>
    public static PolicyRequest DecodeRequest(byte[] bytes)
    {
        try
        {
            var r = new ByteReader(bytes);
            byte op = r.ReadByte();
            PolicyRequest request;
            switch (op)
            {
                case OpMint:
                {
                    string identifier = r.ReadString();
                    string location = r.ReadString();
                    ulong count = VarInt.ReadU64(r);
                    if (count > (ulong)(r.Length - r.Position))
                        throw new CrdtMsgException($"mint caveat count {count} exceeds remaining bytes");
                    var caveats = new List<string>((int)count);
                    for (ulong i = 0; i < count; i++)
                        caveats.Add(r.ReadString());
                    request = new PolicyRequest.Mint(identifier, location, caveats);
                    break;
                }
                case OpAttenuate:
                {
                    ulong len = VarInt.ReadU64(r);
                    if (len > (ulong)(r.Length - r.Position))
                        throw new CrdtMsgException($"attenuate token length {len} exceeds remaining bytes");
                    byte[] token = r.ReadBytes((int)len);
                    string caveat = r.ReadString();
                    request = new PolicyRequest.Attenuate(token, caveat);
                    break;
                }
                default:
                    throw new CrdtMsgException($"unknown policy request op 0x{op:X2} — fail closed");
            }
            if (!r.AtEnd)
                throw new CrdtMsgException($"{r.Length - r.Position} trailing byte(s) after policy request");
            return request;
        }
        catch (Exception ex) when (ex is not CrdtMsgException)
        {
            throw new CrdtMsgException($"malformed policy request: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- reply bodies

    public static byte[] EncodeOkReply(byte[] token)
    {
        var w = new ByteWriter();
        w.WriteByte(StatusOk);
        VarInt.WriteU64(w, (ulong)token.Length);
        w.WriteBytes(token);
        return w.TakeBytes();
    }

    public static byte[] EncodeRefusedReply(string reason)
    {
        var w = new ByteWriter();
        w.WriteByte(StatusRefused);
        w.WriteString(reason);
        return w.TakeBytes();
    }

    /// <summary>Decode a reply body; consume-all-or-throw.</summary>
    public static PolicyReply DecodeReply(byte[] bytes)
    {
        try
        {
            var r = new ByteReader(bytes);
            byte status = r.ReadByte();
            PolicyReply reply;
            switch (status)
            {
                case StatusOk:
                {
                    ulong len = VarInt.ReadU64(r);
                    if (len > (ulong)(r.Length - r.Position))
                        throw new CrdtMsgException($"reply token length {len} exceeds remaining bytes");
                    reply = new PolicyReply.Ok(r.ReadBytes((int)len));
                    break;
                }
                case StatusRefused:
                    reply = new PolicyReply.Refused(r.ReadString());
                    break;
                default:
                    throw new CrdtMsgException($"unknown policy reply status 0x{status:X2} — fail closed");
            }
            if (!r.AtEnd)
                throw new CrdtMsgException($"{r.Length - r.Position} trailing byte(s) after policy reply");
            return reply;
        }
        catch (Exception ex) when (ex is not CrdtMsgException)
        {
            throw new CrdtMsgException($"malformed policy reply: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- envelope

    /// <summary>Wrap a policy body as the single must-understand section of a router-opaque
    /// CrdtModel.None envelope (the E3PC carriage shape).</summary>
    public static Message Envelope(string msgId, string from, string to, long seq, long sectionType, byte[] body) =>
        new(
            SchemaVersion: 1,
            PayloadType: PayloadType.CrdtMessage,
            Header: new Header(msgId, from, to, seq, RoutingPolicy.Empty),
            Sections: new[] { new Section(sectionType, body) },
            CrdtModel: CrdtModel.None);
}
