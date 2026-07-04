// SC-005/011 — whole + sub-content signatures: tamper/remove/reorder detected, transcode survives, and
// the two signature classes are distinct (feature 041-crdtmsg-mvp, T036).

using System.Security.Cryptography;
using GlpRuntime.CrdtMsg.Cap;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.CrdtMsg.Sig;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class SignatureTests
{
    private static Message ThreeBlockMessage() => new(
        1, PayloadType.CrdtMessage,
        new Header("m", "alice", "bob", 0, RoutingPolicy.Empty),
        new[]
        {
            new Section(0x12, new byte[] { 1, 2, 3 }),
            new Section(0x40, new byte[] { 4, 5, 6, 7 }),
            new Section(0x42, new byte[] { 8, 9 }),
        },
        CrdtModel.OpBased);

    private static (byte[] whole, List<byte[]> blocks) Derive(Message m) =>
        (MessageCodec.Canonical(m), m.Sections.Select(s => s.Value).ToList());

    private static (PeerKeyStore signer, PeerKeyStore verifier) Enrolled()
    {
        var signer = new PeerKeyStore("alice");
        var verifier = new PeerKeyStore("bob");
        verifier.Enroll("alice", signer.LocalPublicKey); // per-peer key enrolled at mesh join (FR-019)
        return (signer, verifier);
    }

    [Fact]
    public void Valid_seals_verify()
    {
        var (signer, verifier) = Enrolled();
        var (whole, blocks) = Derive(ThreeBlockMessage());
        var seal = SealSet.Seal(signer, whole, blocks);
        Assert.True(seal.Verify(verifier, whole, blocks));
    }

    [Fact]
    public void Single_byte_tamper_of_whole_fails()
    {
        var (signer, verifier) = Enrolled();
        var (whole, blocks) = Derive(ThreeBlockMessage());
        var seal = SealSet.Seal(signer, whole, blocks);
        whole[5] ^= 0x01;
        Assert.False(seal.Verify(verifier, whole, blocks));
    }

    [Fact]
    public void Sub_block_tamper_fails()
    {
        var (signer, verifier) = Enrolled();
        var (whole, blocks) = Derive(ThreeBlockMessage());
        var seal = SealSet.Seal(signer, whole, blocks);
        blocks[1][0] ^= 0xFF;
        Assert.False(seal.Verify(verifier, whole, blocks));
    }

    [Fact]
    public void Sub_block_removal_fails()
    {
        var (signer, verifier) = Enrolled();
        var (whole, blocks) = Derive(ThreeBlockMessage());
        var seal = SealSet.Seal(signer, whole, blocks);
        blocks.RemoveAt(1);
        Assert.False(seal.Verify(verifier, whole, blocks));
    }

    [Fact]
    public void Sub_block_reorder_fails()
    {
        var (signer, verifier) = Enrolled();
        var (whole, blocks) = Derive(ThreeBlockMessage());
        var seal = SealSet.Seal(signer, whole, blocks);
        (blocks[0], blocks[2]) = (blocks[2], blocks[0]);
        Assert.False(seal.Verify(verifier, whole, blocks));
    }

    [Fact]
    public void Signatures_survive_transcode_across_all_four_surfaces()
    {
        var (signer, verifier) = Enrolled();
        var msg = ThreeBlockMessage();
        var (whole, blocks) = Derive(msg);
        var seal = SealSet.Seal(signer, whole, blocks);

        foreach (var surface in MessageCodec.Surfaces)
        {
            Message rt = surface.Decode(surface.Encode(msg));
            var (whole2, blocks2) = Derive(rt);
            Assert.True(seal.Verify(verifier, whole2, blocks2), $"seal broke after {surface.Name} transcode");
        }
    }

    [Fact]
    public void Content_and_capability_classes_are_distinct()
    {
        // T041: attenuating a capability (HMAC) does not touch the content seals (Ed25519).
        var (signer, verifier) = Enrolled();
        var (whole, blocks) = Derive(ThreeBlockMessage());
        var seal = SealSet.Seal(signer, whole, blocks);

        var rootKey = RandomNumberGenerator.GetBytes(32);
        var cap = Macaroon.Create(rootKey, "mesh", "cap").AddCaveat(new Caveat("op", "=", "insert"));
        var attenuated = cap.AddCaveat(new Caveat("peer", "=", "bob"));

        Assert.NotEqual(cap.Signature, attenuated.Signature);       // capability changed
        Assert.True(seal.Verify(verifier, whole, blocks));          // content attestation unaffected
    }
}
