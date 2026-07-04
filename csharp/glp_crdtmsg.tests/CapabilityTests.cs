// SC-006 — capability verify-before-act, fail-closed, refusal recorded (feature 041-crdtmsg-mvp, T035).

using System.Security.Cryptography;
using GlpRuntime.CrdtMsg.Cap;
using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class CapabilityTests
{
    private static readonly byte[] RootKey = RandomNumberGenerator.GetBytes(32);
    private static readonly IReadOnlySet<string> Understood = new HashSet<string> { "op", "peer", "expires" };

    private static Macaroon InsertCap() =>
        Macaroon.Create(RootKey, "mesh", "cap-1").AddCaveat(new Caveat("op", "=", "insert"));

    [Fact]
    public void Satisfiable_capability_verifies()
    {
        var ctx = new Dictionary<string, string> { ["op"] = "insert" };
        Assert.True(InsertCap().Verify(RootKey, ctx, Understood));
    }

    [Fact]
    public void Unsatisfiable_caveat_fails_closed_and_is_recorded_as_refusal()
    {
        var ctx = new Dictionary<string, string> { ["op"] = "delete" }; // caveat wants insert
        var cap = InsertCap();
        var log = new ProvenanceLog();

        bool ok = cap.Verify(RootKey, ctx, Understood);
        if (!ok)
            log.Record("alice", "bob", DateTimeOffset.UnixEpoch, "deadbeef", ProvenanceOutcome.Refused);

        Assert.False(ok);
        Assert.Single(log.Refusals); // refusal recorded, never a silent drop (SC-006)
    }

    [Fact]
    public void Un_understood_caveat_fails_closed()
    {
        var cap = Macaroon.Create(RootKey, "mesh", "cap-2").AddCaveat(new Caveat("weird", "=", "x"));
        var ctx = new Dictionary<string, string> { ["weird"] = "x" }; // even though satisfiable...
        Assert.False(cap.Verify(RootKey, ctx, Understood)); // ...the key is not understood ⇒ fail-closed
    }

    [Fact]
    public void Tampered_capability_signature_fails()
    {
        var cap = InsertCap();
        var ctx = new Dictionary<string, string> { ["op"] = "insert" };
        cap.Signature[0] ^= 0xFF;
        Assert.False(cap.Verify(RootKey, ctx, Understood));
    }

    [Fact]
    public void Numeric_expiry_caveat()
    {
        var cap = Macaroon.Create(RootKey, "mesh", "cap-3").AddCaveat(new Caveat("expires", ">", "100"));
        Assert.True(cap.Verify(RootKey, new Dictionary<string, string> { ["expires"] = "150" }, Understood));
        Assert.False(cap.Verify(RootKey, new Dictionary<string, string> { ["expires"] = "50" }, Understood));
    }

    [Fact]
    public void Amulet_slot_roundtrips_and_rejects_legacy_16_bytes()
    {
        var amulet = new Amulet(0x0102_0304_0506, 0x00AB_CDEF & 0xFFFFFF, 0x0F, RandomNumberGenerator.GetBytes(16));
        var decoded = Amulet.Decode(amulet.Encode());
        Assert.Equal(amulet.Port, decoded.Port);
        Assert.Equal(amulet.ObjNum, decoded.ObjNum);
        Assert.Equal(amulet.Rights, decoded.Rights);
        Assert.Equal(amulet.Check, decoded.Check);

        Assert.Throws<CrdtMsgException>(() => Amulet.Decode(new byte[16])); // legacy literal 16-byte rejected
        Assert.Throws<CrdtMsgException>(() => new Amulet(0, 0, 0, new byte[8]).Encode()); // Check < 128b
    }
}
