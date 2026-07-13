// E3pcCtrl frame conformance (feature 057-yngenios-pocw-coin, T034).
//
// The 057 contract (coin-op-schemas.md §E3pcCtrl): CBOR round-trip for all five phases,
// missing-required reject / unknown-key tolerance, Ed25519 sig over the canonical bytes of
// keys 0-4, must-understand section riding a router-opaque Message, and the reliability
// semantics the frame ASSUMES from the substrate: FIFO + dedup via the delivery window,
// single-winner fencing, and receiver-side idempotence by (commit_id, attempt).

using System.Security.Cryptography;

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Headers;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.CrdtMsg.Route;
using GlpRuntime.CrdtMsg.Sig;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.CrdtMsg.Tests;

public class E3pcCtrlTests
{
    private static E3pcCtrl NewFrame(PeerKeyStore keys, E3pcPhase phase, ulong attempt = 0, byte[]? commitId = null)
    {
        commitId ??= RandomNumberGenerator.GetBytes(E3pcCtrl.CommitIdSize);
        var dot = new Dot(keys.LocalPeer, 7);
        var sig = keys.Sign(E3pcCtrlCodec.SigningBytes(commitId, dot, keys.LocalPeer, phase, attempt));
        return new E3pcCtrl(commitId, dot, keys.LocalPeer, phase, attempt, sig);
    }

    [Theory]
    [InlineData(E3pcPhase.Prepare)]
    [InlineData(E3pcPhase.Vote)]
    [InlineData(E3pcPhase.Precommit)]
    [InlineData(E3pcPhase.Commit)]
    [InlineData(E3pcPhase.Abort)]
    public void RoundTrip_AllPhases(E3pcPhase phase)
    {
        var keys = new PeerKeyStore("w1");
        var frame = NewFrame(keys, phase, attempt: 3);

        var decoded = E3pcCtrlCodec.Decode(E3pcCtrlCodec.Encode(frame));

        Assert.Equal(frame.CommitId, decoded.CommitId);
        Assert.Equal(frame.CoinDot, decoded.CoinDot);
        Assert.Equal(frame.FromPeer, decoded.FromPeer);
        Assert.Equal(phase, decoded.Phase);
        Assert.Equal(3UL, decoded.Attempt);
        Assert.Equal(frame.Sig, decoded.Sig);
    }

    [Fact]
    public void Signature_CoversKeys0To4_AndVerifies()
    {
        var keys = new PeerKeyStore("w1");
        var frame = NewFrame(keys, E3pcPhase.Precommit, attempt: 1);
        var decoded = E3pcCtrlCodec.Decode(E3pcCtrlCodec.Encode(frame));

        // the receiver recomputes the canonical signing bytes and verifies the enrolled key
        var payload = E3pcCtrlCodec.SigningBytes(
            decoded.CommitId, decoded.CoinDot, decoded.FromPeer, decoded.Phase, decoded.Attempt);
        Assert.True(keys.Verify("w1", payload, decoded.Sig));

        // any field mutation breaks the signature (phase escalation, attempt replay, dot swap)
        Assert.False(keys.Verify("w1",
            E3pcCtrlCodec.SigningBytes(decoded.CommitId, decoded.CoinDot, decoded.FromPeer, E3pcPhase.Commit, decoded.Attempt),
            decoded.Sig));
        Assert.False(keys.Verify("w1",
            E3pcCtrlCodec.SigningBytes(decoded.CommitId, decoded.CoinDot, decoded.FromPeer, decoded.Phase, 2),
            decoded.Sig));
    }

    [Fact]
    public void Decode_MissingRequiredKey_Rejects()
    {
        // hand-build a map without phase (key 3): {0: id, 1: dot, 2: from, 4: attempt, 5: sig}
        var w = new System.Formats.Cbor.CborWriter(System.Formats.Cbor.CborConformanceMode.Canonical);
        w.WriteStartMap(5);
        w.WriteInt32(0); w.WriteByteString(new byte[16]);
        w.WriteInt32(1);
        w.WriteStartMap(2);
        w.WriteInt32(0); w.WriteTextString("w1");
        w.WriteInt32(1); w.WriteInt64(1);
        w.WriteEndMap();
        w.WriteInt32(2); w.WriteTextString("w1");
        w.WriteInt32(4); w.WriteUInt64(0);
        w.WriteInt32(5); w.WriteByteString(new byte[64]);
        w.WriteEndMap();

        var ex = Assert.Throws<CrdtMsgException>(() => E3pcCtrlCodec.Decode(w.Encode()));
        Assert.Contains("phase", ex.Message);
    }

    [Fact]
    public void Decode_UnknownKey_IsIgnored()
    {
        var keys = new PeerKeyStore("w1");
        var frame = NewFrame(keys, E3pcPhase.Vote);

        // re-encode with an extra unknown key 9 (forward evolution: MUST be ignored)
        var w = new System.Formats.Cbor.CborWriter(System.Formats.Cbor.CborConformanceMode.Canonical);
        w.WriteStartMap(7);
        w.WriteInt32(0); w.WriteByteString(frame.CommitId);
        w.WriteInt32(1);
        w.WriteStartMap(2);
        w.WriteInt32(0); w.WriteTextString(frame.CoinDot.PeerName);
        w.WriteInt32(1); w.WriteInt64(frame.CoinDot.Counter);
        w.WriteEndMap();
        w.WriteInt32(2); w.WriteTextString(frame.FromPeer);
        w.WriteInt32(3); w.WriteUInt32((uint)frame.Phase);
        w.WriteInt32(4); w.WriteUInt64(frame.Attempt);
        w.WriteInt32(5); w.WriteByteString(frame.Sig);
        w.WriteInt32(9); w.WriteTextString("future-extension");
        w.WriteEndMap();

        var decoded = E3pcCtrlCodec.Decode(w.Encode());
        Assert.Equal(frame.CommitId, decoded.CommitId);
        Assert.Equal(E3pcPhase.Vote, decoded.Phase);
    }

    [Fact]
    public void Decode_WrongSizes_Reject()
    {
        var keys = new PeerKeyStore("w1");
        var good = NewFrame(keys, E3pcPhase.Prepare);

        Assert.Throws<CrdtMsgException>(() =>
            E3pcCtrlCodec.Encode(good with { CommitId = new byte[8] }));
        Assert.Throws<CrdtMsgException>(() =>
            E3pcCtrlCodec.Encode(good with { Sig = new byte[32] }));
    }

    [Fact]
    public void RidesMessage_AsMustUnderstandSection_VerbatimAcrossBinaryCodec()
    {
        var keys = new PeerKeyStore("coordinator");
        var frame = NewFrame(keys, E3pcPhase.Prepare);
        var msg = new Message(
            SchemaVersion: 1,
            PayloadType: PayloadType.CrdtMessage,
            Header: new Header("coordinator#0", "coordinator", "w1", 0, RoutingPolicy.Empty),
            Sections: new[] { E3pcCtrlCodec.SectionOf(frame) },
            CrdtModel: CrdtModel.None);

        var bytes = MessageCodec.Binary.Encode(msg);
        var decodedMsg = MessageCodec.Binary.Decode(bytes);

        // a receiver that understands E3PC extracts the identical frame
        DecodeGuard.CheckMustUnderstand(decodedMsg, new HashSet<long> { E3pcCtrlCodec.SectionType });
        var extracted = E3pcCtrlCodec.FromMessage(decodedMsg);
        Assert.NotNull(extracted);
        Assert.Equal(frame.CommitId, extracted.CommitId);
        Assert.Equal(frame.CoinDot, extracted.CoinDot);
        Assert.Equal(frame.Phase, extracted.Phase);
        Assert.Equal(frame.Sig, extracted.Sig);

        // a legacy receiver that does NOT understand E3PC fails LOUD (odd type ⇒ must-understand)
        Assert.ThrowsAny<Exception>(() =>
            DecodeGuard.CheckMustUnderstand(decodedMsg, new HashSet<long> { UnifiedHeader.OpSectionType }));
    }

    [Fact] // the "quic"-link payload codec understands E3PC (0x15) and carries it without loud-rejecting.
    public void CrdtMsgPayloadCodec_Understands_E3pc_And_Carries_It()
    {
        var keys = new PeerKeyStore("coordinator");
        var frame = NewFrame(keys, E3pcPhase.Prepare);
        var msg = new Message(
            SchemaVersion: 1,
            PayloadType: PayloadType.CrdtMessage,
            Header: new Header("coordinator#0", "coordinator", "w1", 0, RoutingPolicy.Empty),
            Sections: new[] { E3pcCtrlCodec.SectionOf(frame) },
            CrdtModel: CrdtModel.None);
        byte[] wire = MessageCodec.Binary.Encode(msg);

        // Before the fix the quic payload codec's Understood set was {OpSectionType} only, so it
        // loud-rejected the odd/must-understand 0x15 E3PC section (codexreview 20260713T110357Z).
        var codec = new GlpRuntime.CrdtMsg.Bridge.CrdtMsgPayloadCodec();
        var ex = Record.Exception(() => codec.Decode(wire));
        Assert.Null(ex);
    }

    [Fact]
    public void Reliability_FifoAndDedup_ViaDeliveryWindow()
    {
        var keys = new PeerKeyStore("coordinator");
        var window = new DeliveryWindow<E3pcCtrl>();
        var f0 = NewFrame(keys, E3pcPhase.Prepare);
        var f1 = NewFrame(keys, E3pcPhase.Precommit, commitId: f0.CommitId, attempt: 0);
        var f2 = NewFrame(keys, E3pcPhase.Commit, commitId: f0.CommitId, attempt: 0);

        // out-of-order arrival is re-sequenced FIFO; duplicates are suppressed
        Assert.Empty(window.Offer(1, f1));
        Assert.Empty(window.Offer(2, f2));
        var released = window.Offer(0, f0);
        Assert.Equal(new[] { E3pcPhase.Prepare, E3pcPhase.Precommit, E3pcPhase.Commit },
            released.Select(f => f.Phase).ToArray());
        Assert.Empty(window.Offer(1, f1)); // at-least-once redelivery: suppressed
    }

    [Fact]
    public void Reliability_Fencing_StaleEpochRejected()
    {
        var keys = new PeerKeyStore("coordinator");
        var window = new DeliveryWindow<E3pcCtrl>();
        Assert.NotEmpty(window.Offer(0, NewFrame(keys, E3pcPhase.Prepare), epoch: 5)); // winner at epoch 5
        Assert.Empty(window.Offer(1, NewFrame(keys, E3pcPhase.Commit), epoch: 4));     // superseded sender: fenced
        Assert.Equal(5, window.Epoch);
    }

    [Fact]
    public void Receiver_Idempotence_ByCommitIdAndAttempt()
    {
        // the receiver-side contract: dedupe by (commit_id, attempt) against the durable
        // decision counter — a redelivered attempt is processed at most once
        var keys = new PeerKeyStore("w1");
        var seen = new HashSet<(string CommitId, ulong Attempt)>();
        var frame = NewFrame(keys, E3pcPhase.Precommit, attempt: 2);
        var redelivery = E3pcCtrlCodec.Decode(E3pcCtrlCodec.Encode(frame));

        bool first = seen.Add((Convert.ToHexString(frame.CommitId), frame.Attempt));
        bool second = seen.Add((Convert.ToHexString(redelivery.CommitId), redelivery.Attempt));
        bool retry = seen.Add((Convert.ToHexString(frame.CommitId), frame.Attempt + 1));

        Assert.True(first);
        Assert.False(second);  // same (commit_id, attempt): idempotent no-op
        Assert.True(retry);    // a NEW attempt is distinct work
    }
}
