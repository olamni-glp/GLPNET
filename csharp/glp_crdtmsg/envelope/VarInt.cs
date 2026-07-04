// LEB128 unsigned varint over the reused ByteWriter/ByteReader (feature 041-crdtmsg-mvp, T013 support).
//
// Contract C2 / data-model §3 (BB-ENC-3): TLV type_number and length are LEB128. This is a proper
// u64 LEB128 (not the int-limited ByteWriter.WriteVarUInt) so type_number can be a full long, and it
// loud-fails on an overlong / truncated encoding (FR-005).

using GlpRuntime.ResultCodec;

namespace GlpRuntime.CrdtMsg.Envelope;

public static class VarInt
{
    /// <summary>Write <paramref name="v"/> as unsigned LEB128 (7 bits/byte, low group first).</summary>
    public static void WriteU64(ByteWriter w, ulong v)
    {
        while (v >= 0x80)
        {
            w.WriteByte((byte)(v | 0x80));
            v >>= 7;
        }
        w.WriteByte((byte)v);
    }

    /// <summary>Read an unsigned LEB128 u64; loud-fail on overlong / overflow / truncation. Enforces a
    /// single canonical representation (rejects trailing-zero padding and 10th-group value overflow), so
    /// the shared TLV type_number/length parser cannot silently accept a corrupt or non-canonical varint
    /// (the canonical-determinism + FR-005 loud-fail invariant).</summary>
    public static ulong ReadU64(ByteReader r)
    {
        ulong result = 0;
        int shift = 0;
        while (true)
        {
            if (r.AtEnd)
                throw new CrdtMsgException("Truncated LEB128: input ended mid-varint");
            byte b = r.ReadByte();
            if (shift == 63)
            {
                // 10th group: only bit 63 is representable. Any other value bit (0x02..0x7E) or a
                // continuation bit (0x80) is overflow; a 0x00 group is overlong padding.
                if ((b & 0xFE) != 0)
                    throw new CrdtMsgException("Corrupt LEB128: value exceeds 64 bits");
                if (b == 0)
                    throw new CrdtMsgException("Corrupt LEB128: non-canonical overlong final group");
                return result | (1UL << 63);
            }
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                // Reject a non-canonical overlong final group (continuation into an all-zero group).
                if (b == 0 && shift > 0)
                    throw new CrdtMsgException("Corrupt LEB128: non-canonical trailing zero group");
                return result;
            }
            shift += 7;
        }
    }
}
