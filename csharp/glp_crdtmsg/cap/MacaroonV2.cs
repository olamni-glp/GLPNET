// Macaroon-v2 — the frozen 056 C2/C3 mesh capability token (feature 058 T035).
//
// Contract s4-policy-service.md §B (056 contract C2, architecture NOT reopened): a token is
// identifier + location + ORDERED caveat strings + a tail HMAC-SHA256 chain:
//
//   sig_0 = HMAC(root_key, identifier_utf8)
//   sig_i = HMAC(sig_{i-1}, caveat_semantic_bytes_i)     (tail = Signature)
//
// where the caveat semantic bytes are the raw UTF-8 of the C3 CLOSED-VOCABULARY caveat string —
// NOT the legacy length-prefixed {key, op, value} triple encoding (cap/Macaroon.cs Caveat.Bytes()).
// This dialect supersedes the legacy {action, peer, expires} gate dialect for minted-for-mesh
// tokens; the legacy gate keeps working where it is used (MacaroonLinkGate) — per-repo cutover
// semantics (fail-closed dialect-mismatch refusal, AS-3) are a LATER task, not this one.
//
// C3 closed vocabulary (any caveat outside it is refused FAIL-CLOSED at mint, attenuate, AND verify):
//   "time<{expiry}"  "resource={hash}"  "key={k}"  "key_prefix={p}"
//   "op={get|put|del|subscribe}"  "keyspace={scope}"

using System.Security.Cryptography;
using System.Text;

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.CrdtMsg.Cap;

/// <summary>The C3 closed caveat vocabulary (056 contract C3). Membership is checked FAIL-CLOSED at
/// mint, attenuate, and verify — an out-of-vocabulary caveat is always a refusal, never a skip.</summary>
public static class MacaroonV2Vocabulary
{
    /// <summary>The closed op set of the <c>op=</c> caveat.</summary>
    public static readonly IReadOnlySet<string> Ops = new HashSet<string> { "get", "put", "del", "subscribe" };

    /// <summary>True iff <paramref name="caveat"/> is a well-formed member of the C3 closed vocabulary.</summary>
    public static bool IsValid(string? caveat)
    {
        if (string.IsNullOrEmpty(caveat)) return false;
        if (caveat.StartsWith("time<", StringComparison.Ordinal))
            return long.TryParse(caveat.AsSpan("time<".Length), out _);
        if (caveat.StartsWith("op=", StringComparison.Ordinal))
            return Ops.Contains(caveat["op=".Length..]);
        foreach (var prefix in new[] { "resource=", "key=", "key_prefix=", "keyspace=" })
        {
            if (caveat.StartsWith(prefix, StringComparison.Ordinal))
                return caveat.Length > prefix.Length;   // a non-empty value is required
        }
        return false;   // not in the closed vocabulary ⇒ fail-closed
    }

    /// <summary>Loud-fail guard: throws <see cref="CrdtMsgException"/> on an out-of-vocabulary caveat.</summary>
    public static void Require(string? caveat)
    {
        if (!IsValid(caveat))
            throw new CrdtMsgException(
                $"caveat '{caveat}' is outside the C3 closed vocabulary "
                + "{time<N, resource=…, key=…, key_prefix=…, op=get|put|del|subscribe, keyspace=…} — fail closed");
    }
}

/// <summary>A macaroon-v2 capability token (056 C2 wire format). Immutable; attenuation returns a NEW
/// token whose HMAC chain extends this one's. The root key appears only at <see cref="Mint"/> and
/// <see cref="VerifySignature"/> — a token holder can attenuate but never broaden.</summary>
public sealed class MacaroonV2
{
    public string Location { get; }
    public string Identifier { get; }
    /// <summary>ORDERED C3 caveat strings — order is part of the signature chain.</summary>
    public IReadOnlyList<string> Caveats { get; }
    /// <summary>The tail HMAC-SHA256 of the chain.</summary>
    public byte[] Signature { get; }

    private MacaroonV2(string location, string identifier, IReadOnlyList<string> caveats, byte[] signature)
    {
        Location = location;
        Identifier = identifier;
        Caveats = caveats;
        Signature = signature;
    }

    /// <summary>Mint a token: <c>sig_0 = HMAC(rootKey, identifier_utf8)</c>, then one chain step per
    /// caveat. Every caveat is vocabulary-checked FAIL-CLOSED before it enters the chain.</summary>
    public static MacaroonV2 Mint(byte[] rootKey, string location, string identifier, IReadOnlyList<string> caveats)
    {
        ArgumentNullException.ThrowIfNull(rootKey);
        if (rootKey.Length == 0)
            throw new CrdtMsgException("macaroon-v2 root key must be non-empty — fail closed");
        if (string.IsNullOrEmpty(identifier))
            throw new CrdtMsgException("macaroon-v2 identifier must be non-empty — fail closed");
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(caveats);

        byte[] sig = HMACSHA256.HashData(rootKey, Encoding.UTF8.GetBytes(identifier));
        foreach (var caveat in caveats)
        {
            MacaroonV2Vocabulary.Require(caveat);
            sig = HMACSHA256.HashData(sig, Encoding.UTF8.GetBytes(caveat));
        }
        return new MacaroonV2(location, identifier, caveats.ToArray(), sig);
    }

    /// <summary>Attenuate: append one C3 caveat, extending the chain — a NEW, strictly-narrower token.
    /// Needs NO root key (the chain extends from the tail), which is exactly why the chain is safe to
    /// hand to callers: they can narrow, never broaden.</summary>
    public MacaroonV2 Attenuate(string caveat)
    {
        MacaroonV2Vocabulary.Require(caveat);
        byte[] sig = HMACSHA256.HashData(Signature, Encoding.UTF8.GetBytes(caveat));
        var caveats = new List<string>(Caveats) { caveat };
        return new MacaroonV2(Location, Identifier, caveats, sig);
    }

    /// <summary>Rehydrate a token received over a transport. The signature is the WIRE'S CLAIM,
    /// carried verbatim — never recomputed here — so <see cref="VerifySignature"/> catches tampering
    /// by failing the fixed-time chain comparison.</summary>
    public static MacaroonV2 FromWire(
        string location, string identifier, IReadOnlyList<string> caveats, byte[] signature) =>
        new(location, identifier, caveats, signature);

    /// <summary>
    /// Recompute the full HMAC chain from <paramref name="rootKey"/> and compare fixed-time against
    /// the carried tail. Fail-closed: an out-of-vocabulary caveat is a refusal even if the chain
    /// would match. NOTE this is chain INTEGRITY + vocabulary only — contextual satisfaction
    /// (clock vs <c>time&lt;</c>, key/op/keyspace scope) is the ENFORCEMENT POINT's job (yngenios is
    /// verify-side per the E-5 ruling); the S4 endpoint uses this to refuse forged/tampered tokens.
    /// </summary>
    public bool VerifySignature(byte[] rootKey)
    {
        ArgumentNullException.ThrowIfNull(rootKey);
        byte[] sig = HMACSHA256.HashData(rootKey, Encoding.UTF8.GetBytes(Identifier));
        foreach (var caveat in Caveats)
        {
            if (!MacaroonV2Vocabulary.IsValid(caveat)) return false;   // un-understood ⇒ fail-closed
            sig = HMACSHA256.HashData(sig, Encoding.UTF8.GetBytes(caveat));
        }
        return CryptographicOperations.FixedTimeEquals(sig, Signature);
    }
}

/// <summary>Serializes a <see cref="MacaroonV2"/> using the shipped byte conventions
/// (ByteWriter/ByteReader: LEB128 varints, varint-length-prefixed UTF-8 strings — the same container
/// conventions as <see cref="MacaroonCodec"/>). Decode is consume-all-or-throw (loud-fail, FR-007
/// discipline): truncation, trailing bytes, or a garbled field is a <see cref="CrdtMsgException"/>,
/// never a silent partial token.</summary>
public static class MacaroonV2Codec
{
    public static byte[] Encode(MacaroonV2 m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var w = new ByteWriter();
        w.WriteString(m.Location);
        w.WriteString(m.Identifier);
        VarInt.WriteU64(w, (ulong)m.Caveats.Count);
        foreach (var caveat in m.Caveats)
            w.WriteString(caveat);
        VarInt.WriteU64(w, (ulong)m.Signature.Length);
        w.WriteBytes(m.Signature);
        return w.TakeBytes();
    }

    /// <summary>Decode the token bytes; consume-all-or-throw. The signature is carried verbatim
    /// (<see cref="MacaroonV2.FromWire"/>) so <see cref="MacaroonV2.VerifySignature"/> catches tampering.</summary>
    public static MacaroonV2 Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            var r = new ByteReader(bytes);
            string location = r.ReadString();
            string identifier = r.ReadString();

            ulong caveatCount = VarInt.ReadU64(r);
            if (caveatCount > (ulong)(r.Length - r.Position))
                throw new CrdtMsgException($"macaroon-v2 caveat count {caveatCount} exceeds remaining bytes");
            var caveats = new List<string>((int)caveatCount);
            for (ulong i = 0; i < caveatCount; i++)
                caveats.Add(r.ReadString());

            ulong sigLen = VarInt.ReadU64(r);
            if (sigLen > (ulong)(r.Length - r.Position))
                throw new CrdtMsgException($"macaroon-v2 signature length {sigLen} exceeds remaining bytes");
            byte[] signature = r.ReadBytes((int)sigLen);

            if (!r.AtEnd)
                throw new CrdtMsgException($"{r.Length - r.Position} trailing byte(s) after macaroon-v2 token");
            return MacaroonV2.FromWire(location, identifier, caveats, signature);
        }
        catch (Exception ex) when (ex is not CrdtMsgException)
        {
            // ByteReader truncation/format faults surface as the one loud-fail decode exception class.
            throw new CrdtMsgException($"malformed macaroon-v2 token: {ex.Message}");
        }
    }
}
