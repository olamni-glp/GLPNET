// E3PC control-frame vocabulary (feature 057-yngenios-pocw-coin, T034).
//
// Contract (057 contracts/coin-op-schemas.md §E3pcCtrl): a NEW frame kind in the
// QuicLinkTransport/UnifiedHeader reserved section carrying one E3PC phase message for the
// coin's gated spend leg. CBOR map with integer keys:
//   {0: commit_id(16B), 1: coin_dot{0: peer, 1: counter}, 2: from_peer,
//    3: phase(uint: 1 prepare | 2 vote | 3 precommit | 4 commit | 5 abort),
//    4: attempt(uint), 5: sig(64B Ed25519 over the canonical bytes of keys 0-4)}
// Unknown keys MUST be ignored (forward evolution); missing required keys MUST reject.
// Semantics: rides the existing glpnet reliability (at-least-once + FIFO window + fencing +
// CRC-32 at the frame layer); the RECEIVER dedupes by (commit_id, attempt) against its durable
// decision counter — the frame layer stays stateless. Only the non-commutative spend leg uses
// these frames. The vocabulary is transport-agnostic: v1 runs over an in-process loopback
// transport, cross-host QUIC uses the same bytes unchanged.

using System.Formats.Cbor;

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Model;

namespace GlpRuntime.CrdtMsg.Route;

/// <summary>The five E3PC phases of one spend commit round.</summary>
public enum E3pcPhase : uint
{
    Prepare = 1,
    Vote = 2,
    Precommit = 3,
    Commit = 4,
    Abort = 5,
}

/// <summary>
/// One E3PC control frame. <c>Sig</c> is a detached 64-byte Ed25519 signature over
/// <see cref="E3pcCtrlCodec.SigningBytes"/> (the canonical CBOR of keys 0-4) — signed by the
/// sender under its enrolled per-peer key (never the layer-0 membership cert, FR-019).
/// </summary>
public sealed record E3pcCtrl(
    byte[] CommitId,     // 16 B spend commit id
    Dot CoinDot,         // the spend op's dot (peer, counter)
    string FromPeer,
    E3pcPhase Phase,
    ulong Attempt,       // retry ordinal; receiver idempotence key is (commit_id, attempt)
    byte[] Sig)          // 64 B Ed25519 over canonical keys 0-4
{
    public const int CommitIdSize = 16;
    public const int SigSize = 64;
}

/// <summary>
/// CBOR codec + section plumbing for <see cref="E3pcCtrl"/>. The frame rides a Message as a
/// reserved MUST-UNDERSTAND section (odd type number) so a legacy reader fails loud rather than
/// silently dropping a commit decision.
/// </summary>
public static class E3pcCtrlCodec
{
    /// <summary>Reserved section type for E3PC control frames. Odd ⇒ must-understand.</summary>
    public const long SectionType = 0x15;

    public static byte[] Encode(E3pcCtrl frame)
    {
        var w = new CborWriter(CborConformanceMode.Canonical);
        w.WriteStartMap(6);
        WriteCore(w, frame);
        w.WriteInt32(5);
        w.WriteByteString(Validated(frame.Sig, E3pcCtrl.SigSize, "sig"));
        w.WriteEndMap();
        return w.Encode();
    }

    /// <summary>The canonical bytes the Ed25519 signature covers: the CBOR map of keys 0-4 only.</summary>
    public static byte[] SigningBytes(byte[] commitId, Dot coinDot, string fromPeer, E3pcPhase phase, ulong attempt)
    {
        var w = new CborWriter(CborConformanceMode.Canonical);
        w.WriteStartMap(5);
        WriteCore(w, new E3pcCtrl(commitId, coinDot, fromPeer, phase, attempt, Array.Empty<byte>()));
        w.WriteEndMap();
        return w.Encode();
    }

    private static void WriteCore(CborWriter w, E3pcCtrl frame)
    {
        w.WriteInt32(0);
        w.WriteByteString(Validated(frame.CommitId, E3pcCtrl.CommitIdSize, "commit_id"));
        w.WriteInt32(1);
        w.WriteStartMap(2);
        w.WriteInt32(0); w.WriteTextString(frame.CoinDot.PeerName);
        w.WriteInt32(1); w.WriteInt64(frame.CoinDot.Counter);
        w.WriteEndMap();
        w.WriteInt32(2);
        w.WriteTextString(frame.FromPeer);
        w.WriteInt32(3);
        w.WriteUInt32((uint)frame.Phase);
        w.WriteInt32(4);
        w.WriteUInt64(frame.Attempt);
    }

    /// <summary>
    /// Decode a frame. Missing required keys reject (loud fail); unknown keys are skipped
    /// (forward evolution) — matching the 057 op-schema discipline.
    /// </summary>
    public static E3pcCtrl Decode(ReadOnlyMemory<byte> bytes)
    {
        var r = new CborReader(bytes, CborConformanceMode.Lax);
        byte[]? commitId = null, sig = null;
        string? fromPeer = null, dotPeer = null;
        long? dotCounter = null;
        uint? phase = null;
        ulong? attempt = null;

        try
        {
            r.ReadStartMap();
            while (r.PeekState() != CborReaderState.EndMap)
            {
                long key = r.ReadInt64();
                switch (key)
                {
                    case 0:
                        commitId = r.ReadByteString();
                        break;
                    case 1:
                        r.ReadStartMap();
                        while (r.PeekState() != CborReaderState.EndMap)
                        {
                            long dk = r.ReadInt64();
                            if (dk == 0) dotPeer = r.ReadTextString();
                            else if (dk == 1) dotCounter = r.ReadInt64();
                            else r.SkipValue();
                        }
                        r.ReadEndMap();
                        break;
                    case 2:
                        fromPeer = r.ReadTextString();
                        break;
                    case 3:
                        phase = r.ReadUInt32();
                        break;
                    case 4:
                        attempt = r.ReadUInt64();
                        break;
                    case 5:
                        sig = r.ReadByteString();
                        break;
                    default:
                        r.SkipValue(); // unknown key: ignored, never fatal
                        break;
                }
            }
            r.ReadEndMap();
        }
        catch (Exception ex) when (ex is CborContentException or InvalidOperationException)
        {
            throw new CrdtMsgException($"malformed E3pcCtrl frame: {ex.Message}", ex);
        }

        if (commitId is null) throw new CrdtMsgException("E3pcCtrl frame missing commit_id (key 0)");
        if (dotPeer is null || dotCounter is null) throw new CrdtMsgException("E3pcCtrl frame missing coin_dot (key 1)");
        if (fromPeer is null) throw new CrdtMsgException("E3pcCtrl frame missing from_peer (key 2)");
        if (phase is null) throw new CrdtMsgException("E3pcCtrl frame missing phase (key 3)");
        if (attempt is null) throw new CrdtMsgException("E3pcCtrl frame missing attempt (key 4)");
        if (sig is null) throw new CrdtMsgException("E3pcCtrl frame missing sig (key 5)");
        if (commitId.Length != E3pcCtrl.CommitIdSize)
            throw new CrdtMsgException($"E3pcCtrl commit_id must be {E3pcCtrl.CommitIdSize} bytes, got {commitId.Length}");
        if (sig.Length != E3pcCtrl.SigSize)
            throw new CrdtMsgException($"E3pcCtrl sig must be {E3pcCtrl.SigSize} bytes, got {sig.Length}");
        if (phase is < 1 or > 5)
            throw new CrdtMsgException($"E3pcCtrl phase {phase} outside 1..5");

        return new E3pcCtrl(commitId, new Dot(dotPeer, dotCounter.Value), fromPeer, (E3pcPhase)phase.Value, attempt.Value, sig);
    }

    /// <summary>Wrap a frame as the E3PC section of a router-opaque message (dispatch leg).</summary>
    public static Section SectionOf(E3pcCtrl frame) => new(SectionType, Encode(frame));

    /// <summary>Extract the E3PC frame from a message (null when the message carries none).</summary>
    public static E3pcCtrl? FromMessage(Message m)
    {
        var section = m.Sections.FirstOrDefault(s => s.TypeNumber == SectionType);
        return section is null ? null : Decode(section.Value);
    }

    private static byte[] Validated(byte[] value, int size, string what) =>
        value.Length == size ? value : throw new CrdtMsgException($"E3pcCtrl {what} must be {size} bytes, got {value.Length}");
}
