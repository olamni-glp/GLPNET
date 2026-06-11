using System.Text;

namespace GlpRuntime.IlCodec;

/// <summary>
/// Low-level byte primitives shared by the codec. Counts and lengths use an
/// unsigned LEB128 varint (always non-negative); operand integer fields, register
/// ids, and int64 constants use a fixed little-endian 8-byte <c>long</c>; doubles
/// are written via their exact IEEE-754 bit pattern so NaN/±0 round-trip exactly.
/// </summary>
internal static class ByteIo
{
    public static void WriteVarUInt(BinaryWriter w, ulong v)
    {
        while (v >= 0x80)
        {
            w.Write((byte)(v | 0x80));
            v >>= 7;
        }
        w.Write((byte)v);
    }

    public static ulong ReadVarUInt(BinaryReader r)
    {
        ulong result = 0;
        int shift = 0;
        while (true)
        {
            byte b = r.ReadByte();
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift >= 64) throw new IlCodecException("Corrupt payload: varint exceeds 64 bits");
        }
        return result;
    }

    public static void WriteString(BinaryWriter w, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        WriteVarUInt(w, (ulong)bytes.Length);
        w.Write(bytes);
    }

    public static string ReadString(BinaryReader r)
    {
        int len = checked((int)ReadVarUInt(r));
        var bytes = r.ReadBytes(len);
        if (bytes.Length != len)
            throw new IlCodecException("Truncated payload: string body shorter than declared length");
        return Encoding.UTF8.GetString(bytes);
    }

    public static void WriteDouble(BinaryWriter w, double d) => w.Write(BitConverter.DoubleToInt64Bits(d));

    public static double ReadDouble(BinaryReader r) => BitConverter.Int64BitsToDouble(r.ReadInt64());
}
