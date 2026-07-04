// LEB128 unsigned varint over the reused ByteWriter/ByteReader (feature 041-crdtmsg-mvp, T013 support).
//
// Contract C2 / data-model §3 (BB-ENC-3): TLV type_number and length are LEB128. This is a proper
// u64 LEB128 (not the int-limited ByteWriter.WriteVarUInt) so type_number can be a full long, and it
// loud-fails on an overlong / truncated encoding (FR-005).

using GlpRuntime.ResultCodec;

namespace GlpRuntime.CrdtMsg.Envelope;

internal static class VarInt
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

    /// <summary>Read an unsigned LEB128 u64; loud-fail on overlong (&gt;10 groups) or truncation.</summary>
    public static ulong ReadU64(ByteReader r)
    {
        ulong result = 0;
        int shift = 0;
        while (true)
        {
            if (shift > 63)
                throw new CrdtMsgException("Corrupt LEB128: value exceeds 64 bits");
            if (r.AtEnd)
                throw new CrdtMsgException("Truncated LEB128: input ended mid-varint");
            byte b = r.ReadByte();
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                // Reject a non-canonical overlong final group (a continuation-free 0x80 padding).
                if (b == 0 && shift > 0)
                    throw new CrdtMsgException("Corrupt LEB128: non-canonical trailing zero group");
                return result;
            }
            shift += 7;
        }
    }
}
