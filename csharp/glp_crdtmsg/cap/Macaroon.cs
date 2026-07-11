// Macaroon capability — HMAC caveat chain, fail-closed (feature 041-crdtmsg-mvp, T037).
//
// Contract C14 / research R5: a minimal HMAC-chained macaroon. Every routed action is gated by a
// macaroon whose first-party caveats are FAIL-CLOSED — an unsatisfiable OR un-understood caveat -> fail.
// Verification happens BEFORE acting (the router calls Verify before delivery). A refusal is a distinct,
// recorded provenance outcome (Provenance), never a silent drop (SC-006). This is the capability class
// (HMAC) — DISTINCT from the content-attestation class (Ed25519 seals, T041).

using System.Security.Cryptography;
using System.Text;

namespace GlpRuntime.CrdtMsg.Cap;

/// <summary>A first-party caveat: <c>key op value</c> (e.g. <c>peer = alice</c>, <c>expires &gt; 100</c>).</summary>
public sealed record Caveat(string Key, string Op, string Value)
{
    /// <summary>
    /// Length-prefixed, injective encoding for the HMAC chain (review finding #5): a plain join is
    /// ambiguous (("a b","=","c") vs ("a","b","= c") would collide), so each field is
    /// 4-byte-LE-length-prefixed — no two distinct structured caveats can hash identically.
    /// </summary>
    public byte[] Bytes()
    {
        byte[] k = Encoding.UTF8.GetBytes(Key), o = Encoding.UTF8.GetBytes(Op), v = Encoding.UTF8.GetBytes(Value);
        var buf = new byte[12 + k.Length + o.Length + v.Length];
        int p = Put(buf, 0, k);
        p = Put(buf, p, o);
        Put(buf, p, v);
        return buf;
    }

    private static int Put(byte[] buf, int p, byte[] x)
    {
        buf[p] = (byte)x.Length; buf[p + 1] = (byte)(x.Length >> 8);
        buf[p + 2] = (byte)(x.Length >> 16); buf[p + 3] = (byte)(x.Length >> 24);
        Array.Copy(x, 0, buf, p + 4, x.Length);
        return p + 4 + x.Length;
    }

    public override string ToString() => Key + " " + Op + " " + Value;
}

public sealed class Macaroon
{
    public string Location { get; }
    public string Identifier { get; }
    public IReadOnlyList<Caveat> Caveats { get; }
    public byte[] Signature { get; }

    private Macaroon(string location, string identifier, IReadOnlyList<Caveat> caveats, byte[] signature)
    {
        Location = location;
        Identifier = identifier;
        Caveats = caveats;
        Signature = signature;
    }

    /// <summary>Mint a root macaroon: signature = HMAC(rootKey, identifier).</summary>
    public static Macaroon Create(byte[] rootKey, string location, string identifier)
    {
        byte[] sig = HMACSHA256.HashData(rootKey, Encoding.UTF8.GetBytes(identifier));
        return new Macaroon(location, identifier, Array.Empty<Caveat>(), sig);
    }

    /// <summary>
    /// Rehydrate a macaroon received over a transport (feature 050 US3, <c>MacaroonCodec</c>): the
    /// signature is the WIRE'S CLAIM, carried verbatim — never recomputed here — so <see cref="Verify"/>
    /// detects tampering by failing the HMAC-chain <c>FixedTimeEquals</c>. Additive; minting stays
    /// <see cref="Create"/> + <see cref="AddCaveat"/>.
    /// </summary>
    public static Macaroon FromWire(
        string location, string identifier, IReadOnlyList<Caveat> caveats, byte[] signature) =>
        new(location, identifier, caveats, signature);

    /// <summary>Attenuate: append a first-party caveat, extending the HMAC chain (a NEW macaroon).</summary>
    public Macaroon AddCaveat(Caveat caveat)
    {
        byte[] sig = HMACSHA256.HashData(Signature, caveat.Bytes());
        var caveats = new List<Caveat>(Caveats) { caveat };
        return new Macaroon(Location, Identifier, caveats, sig);
    }

    /// <summary>
    /// Verify BEFORE acting (C14). Fail-closed: the HMAC chain must reproduce the signature (integrity),
    /// and EVERY caveat must be both UNDERSTOOD (its key is in <paramref name="understoodKeys"/>) and
    /// SATISFIED by <paramref name="context"/>. Any unmet or un-understood caveat ⇒ false.
    /// </summary>
    public bool Verify(byte[] rootKey, IReadOnlyDictionary<string, string> context, IReadOnlySet<string> understoodKeys)
    {
        byte[] sig = HMACSHA256.HashData(rootKey, Encoding.UTF8.GetBytes(Identifier));
        foreach (var c in Caveats)
        {
            sig = HMACSHA256.HashData(sig, c.Bytes());
            if (!understoodKeys.Contains(c.Key)) return false;            // un-understood ⇒ fail-closed
            if (!Satisfied(c, context)) return false;                     // unsatisfiable ⇒ fail-closed
        }
        return CryptographicOperations.FixedTimeEquals(sig, Signature);   // integrity (tamper) check
    }

    private static bool Satisfied(Caveat c, IReadOnlyDictionary<string, string> context)
    {
        if (!context.TryGetValue(c.Key, out var actual)) return false;
        return c.Op switch
        {
            "=" => actual == c.Value,
            ">" => long.TryParse(actual, out var a) && long.TryParse(c.Value, out var v) && a > v,
            ">=" => long.TryParse(actual, out var a) && long.TryParse(c.Value, out var v) && a >= v,
            _ => false, // unknown operator ⇒ fail-closed
        };
    }
}
