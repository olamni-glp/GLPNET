// Term sub-codec — feature 038-result-codec-and-framecodec-ride.
//
// Byte primitives + Term encode/decode for the Section-15 *term* portion. Byte
// conventions are IDENTICAL to the shipped 029 GlpRuntime.IlCodec (ByteIo +
// ConstantCodec): unsigned LEB128 varints, fixed 8-byte little-endian int64,
// IEEE-754 double bit pattern, varint+UTF-8 strings, term tags 0x00–0x06. The one
// added tag is 0x07 (unbound VarRef -> GlobalVarId), outside 029's IL scope
// (data-model §3, contract §2–§3). This is a *parallel* implementation that
// reproduces 029's bytes — it does NOT reuse 029 code (FR-007; 029 is the C#
// byte oracle only). Dart is the source of truth (R9); this mirrors
// glp_runtime/lib/codec/term_codec.dart byte-for-byte.
//
// All malformed input fails loudly with ResultCodecException (FR-005, SC-004):
// truncated reads, varints over 64 bits, and unknown / 029-reserved term tags.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GlpRuntime.ResultCodec;

// --- Term tag table (contract §3 / data-model §3) ---
internal static class TermTags
{
    public const byte Null = 0x00;   // 029-compat; not in the GLP envelope term model
    public const byte Bool = 0x01;   // 029-compat; not in the GLP envelope term model
    public const byte Int64 = 0x02;
    public const byte Double = 0x03; // GATED (ED-6 AtomVM /float)
    public const byte String = 0x04;
    public const byte Atom = 0x05;
    public const byte Struct = 0x06;
    public const byte VarRef = 0x07; // new vs 029
}

/// <summary>
/// Append-only byte sink for the codec. Byte primitives are byte-identical to 029
/// <c>ByteIo</c>: int64 via <c>BinaryWriter.Write(long)</c> (always little-endian in
/// .NET), doubles via <c>BitConverter.DoubleToInt64Bits</c>, unsigned LEB128 varints,
/// and varint-length-prefixed UTF-8 strings.
/// </summary>
public sealed class ByteWriter
{
    private readonly MemoryStream _ms = new();
    private readonly BinaryWriter _w;

    public ByteWriter() { _w = new BinaryWriter(_ms); }

    public void WriteByte(byte v) => _w.Write(v);
    public void WriteBytes(byte[] bytes) => _w.Write(bytes);

    /// <summary>Unsigned LEB128 varint (counts / lengths).</summary>
    public void WriteVarUInt(int v)
    {
        if (v < 0)
            throw new ResultCodecException($"Cannot varint-encode a negative count: {v}");
        uint x = (uint)v;
        while (x >= 0x80)
        {
            _w.Write((byte)((x & 0x7F) | 0x80));
            x >>= 7;
        }
        _w.Write((byte)x);
    }

    /// <summary>Fixed 8-byte little-endian int64 (BinaryWriter is always little-endian).</summary>
    public void WriteInt64LE(long v) => _w.Write(v);

    /// <summary>IEEE-754 double bit pattern, 8 bytes little-endian.</summary>
    public void WriteDoubleBits(double d) => _w.Write(BitConverter.DoubleToInt64Bits(d));

    /// <summary>Varint length + UTF-8 bytes (the "0x04-body").</summary>
    public void WriteString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        WriteVarUInt(bytes.Length);
        _w.Write(bytes);
    }

    public byte[] TakeBytes()
    {
        _w.Flush();
        return _ms.ToArray();
    }
}

/// <summary>
/// Cursor over an immutable byte source; every read bounds-checks and fails loudly
/// with <see cref="ResultCodecException"/> (mirrors the Dart <c>BytesReader</c>).
/// </summary>
public sealed class ByteReader
{
    private readonly byte[] _data;
    private int _pos;

    public ByteReader(byte[] data) { _data = data; _pos = 0; }

    public int Position => _pos;
    public int Length => _data.Length;
    public bool AtEnd => _pos >= _data.Length;

    public byte ReadByte()
    {
        if (_pos >= _data.Length)
            throw new ResultCodecException(
                "Truncated payload: reached end of input before a byte was read");
        return _data[_pos++];
    }

    public byte[] ReadBytes(int n)
    {
        if (n < 0)
            throw new ResultCodecException($"Negative read length: {n}");
        if (n > _data.Length - _pos)
            throw new ResultCodecException(
                $"Truncated payload: need {n} byte(s) but only {_data.Length - _pos} remain");
        var outBytes = new byte[n];
        Array.Copy(_data, _pos, outBytes, 0, n);
        _pos += n;
        return outBytes;
    }

    /// <summary>Unsigned LEB128 varint; loud-fail if it exceeds 64 bits (contract §5.7).</summary>
    public int ReadVarUInt()
    {
        ulong result = 0;
        int shift = 0;
        while (true)
        {
            byte b = ReadByte();
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift >= 64)
                throw new ResultCodecException("Corrupt payload: varint exceeds 64 bits");
        }
        if (result > int.MaxValue)
            throw new ResultCodecException(
                $"Corrupt payload: varint count {result} exceeds the addressable Int32 range");
        return (int)result;
    }

    public long ReadInt64LE()
    {
        var bytes = ReadBytes(8);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }

    public double ReadDoubleBits()
    {
        var bytes = ReadBytes(8);
        return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes));
    }

    public string ReadString()
    {
        int len = ReadVarUInt();
        var bytes = ReadBytes(len);
        return Encoding.UTF8.GetString(bytes);
    }
}

/// <summary>
/// Term / GlobalVarId encode + decode (contract §3). Static, allocation-light, and
/// byte-identical to the Dart reference.
/// </summary>
public static class TermCodec
{
    // --- GlobalVarId wire form (data-model §4): agentId (string body) + localId (int64 LE) ---

    public static void EncodeGlobalVarId(ByteWriter w, GlobalVarId id)
    {
        w.WriteString(id.AgentId);
        w.WriteInt64LE(id.LocalId);
    }

    public static GlobalVarId DecodeGlobalVarId(ByteReader r)
    {
        string agentId = r.ReadString();
        long localId = r.ReadInt64LE();
        return new GlobalVarId(agentId, localId);
    }

    // --- Term encode/decode (contract §3) ---

    public static void EncodeTerm(ByteWriter w, Term t)
    {
        switch (t)
        {
            case ConstTerm ct:
                switch (ct.Value)
                {
                    case ConstInt ci:
                        w.WriteByte(TermTags.Int64);
                        w.WriteInt64LE(ci.Value);
                        break;
                    case ConstReal cr:
                        w.WriteByte(TermTags.Double);
                        w.WriteDoubleBits(cr.Value);
                        break;
                    case ConstString cs:
                        w.WriteByte(TermTags.String);
                        w.WriteString(cs.Value);
                        break;
                    case ConstAtom ca:
                        w.WriteByte(TermTags.Atom);
                        w.WriteString(ca.Name);
                        break;
                    default:
                        throw new ResultCodecException(
                            $"Unknown Constant subtype {ct.Value.GetType().FullName}");
                }
                break;
            case StructTerm st:
                w.WriteByte(TermTags.Struct);
                w.WriteString(st.Functor);
                w.WriteVarUInt(st.Args.Count);
                foreach (var arg in st.Args)
                    EncodeTerm(w, arg);
                break;
            case VarRef vr:
                w.WriteByte(TermTags.VarRef);
                EncodeGlobalVarId(w, vr.Id);
                break;
            default:
                throw new ResultCodecException(
                    $"Unknown Term subtype {t.GetType().FullName}");
        }
    }

    public static Term DecodeTerm(ByteReader r)
    {
        byte tag = r.ReadByte();
        switch (tag)
        {
            case TermTags.Int64:
                return new ConstTerm(new ConstInt(r.ReadInt64LE()));
            case TermTags.Double:
                return new ConstTerm(new ConstReal(r.ReadDoubleBits()));
            case TermTags.String:
                return new ConstTerm(new ConstString(r.ReadString()));
            case TermTags.Atom:
                return new ConstTerm(new ConstAtom(r.ReadString()));
            case TermTags.Struct:
            {
                string functor = r.ReadString();
                int arity = r.ReadVarUInt();
                var args = new List<Term>(); // not pre-sized: a corrupt arity must not pre-allocate
                for (int i = 0; i < arity; i++)
                    args.Add(DecodeTerm(r));
                return new StructTerm(functor, args);
            }
            case TermTags.VarRef:
                return new VarRef(DecodeGlobalVarId(r));
            case TermTags.Null:
            case TermTags.Bool:
                // 0x00/0x01 are reserved in the shared 029 tag space but have no
                // representation in the 034 GLP term model (GLP booleans are atoms ->
                // 0x05). The result-envelope corpus never produces them. Loud-fail
                // rather than invent a null/bool term (mirrors term_codec.dart).
                throw new ResultCodecException(
                    $"Term tag 0x{tag:X2} (029-reserved null/bool) has no representation " +
                    "in the GLP result-envelope term model");
            default:
                throw new ResultCodecException($"Unknown term tag 0x{tag:X2}");
        }
    }
}
