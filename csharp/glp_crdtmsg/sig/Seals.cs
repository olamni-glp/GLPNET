// Whole + sub-content signatures (feature 041-crdtmsg-mvp, T040 + T041).
//
// Contract C17/C18 / research R4:
//   whole_content_sig = Ed25519 over the CANONICAL deterministic binary encoding (MessageCodec.Canonical)
//   — so signatures survive lossless transcode across all four surfaces (SC-011). sub_content_seals[] =
//   per-block seals in a BISCUIT-STYLE append-only chain: seal_i signs (block_i || prev_seal_sig), so any
//   removal, reorder, or single-byte tamper of a signed sub-block breaks verification (SC-005). The seal
//   payload is the Ed25519 signature that a COSE_Sign1 structure would carry (COSE/CBOR framing via R2 is
//   a thin wrapper; the cryptographic core is here).
//
//   T041: this is the CONTENT-attestation class (Ed25519) — categorically DISTINCT from the capability
//   class (macaroon HMAC). Attenuating a capability never touches these seals, and vice versa.

namespace GlpRuntime.CrdtMsg.Sig;

/// <summary>One sub-content seal: the Ed25519 signature over (block || previous-seal-signature).</summary>
public sealed record SubSeal(int BlockIndex, byte[] Signature);

/// <summary>The full signature set over a message: one whole-content sig + a chained per-block seal list.</summary>
public sealed class SealSet
{
    public string Signer { get; }
    public byte[] WholeSig { get; }
    public IReadOnlyList<SubSeal> SubSeals { get; }

    private SealSet(string signer, byte[] wholeSig, IReadOnlyList<SubSeal> subSeals)
    {
        Signer = signer;
        WholeSig = wholeSig;
        SubSeals = subSeals;
    }

    /// <summary>Sign the canonical whole plus each sub-block, chaining the block seals (Biscuit-style).</summary>
    public static SealSet Seal(PeerKeyStore keys, byte[] canonicalWhole, IReadOnlyList<byte[]> blocks)
    {
        byte[] wholeSig = keys.Sign(canonicalWhole);
        var seals = new List<SubSeal>(blocks.Count);
        byte[] prev = Array.Empty<byte>();
        for (int i = 0; i < blocks.Count; i++)
        {
            byte[] sig = keys.Sign(Chain(blocks[i], i, blocks.Count, prev));
            seals.Add(new SubSeal(i, sig));
            prev = sig;
        }
        return new SealSet(keys.LocalPeer, wholeSig, seals);
    }

    /// <summary>
    /// Verify the whole-content sig AND the full sub-content chain. Any tamper / removal / reorder of a
    /// signed block breaks the chain (SC-005). Returns false — never throws — so a refusal is recorded as
    /// a provenance outcome, not an exception.
    /// </summary>
    public bool Verify(PeerKeyStore keys, byte[] canonicalWhole, IReadOnlyList<byte[]> blocks)
    {
        if (!keys.Verify(Signer, canonicalWhole, WholeSig)) return false;
        if (SubSeals.Count != blocks.Count) return false; // a block added or removed
        byte[] prev = Array.Empty<byte>();
        for (int i = 0; i < blocks.Count; i++)
        {
            if (SubSeals[i].BlockIndex != i) return false;                       // reorder
            if (!keys.Verify(Signer, Chain(blocks[i], i, blocks.Count, prev), SubSeals[i].Signature)) return false;
            prev = SubSeals[i].Signature;
        }
        return true;
    }

    // Each seal binds its block, its INDEX, the total block COUNT, and the previous seal. Binding the
    // count makes the chain self-sufficient: dropping trailing block(s)+seal(s) (which keeps Count equal)
    // still fails, because the surviving seals were signed with the original count (review finding #2 —
    // defense-in-depth for a future seal-deserialization path; today WholeSig already spans all blocks).
    private static byte[] Chain(byte[] block, int index, int count, byte[] prevSig)
    {
        var buf = new byte[block.Length + 8 + prevSig.Length];
        Buffer.BlockCopy(block, 0, buf, 0, block.Length);
        WriteInt32LE(buf, block.Length, index);
        WriteInt32LE(buf, block.Length + 4, count);
        Buffer.BlockCopy(prevSig, 0, buf, block.Length + 8, prevSig.Length);
        return buf;
    }

    private static void WriteInt32LE(byte[] b, int off, int v)
    {
        b[off] = (byte)v; b[off + 1] = (byte)(v >> 8); b[off + 2] = (byte)(v >> 16); b[off + 3] = (byte)(v >> 24);
    }
}
