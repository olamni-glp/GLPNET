using Rt = GlpRuntime.Runtime;

namespace GlpRuntime.IlCodec;

/// <summary>
/// Recursive sub-encoder for the closed constant whitelist (data-model + research D1).
/// A constant operand is any <c>object?</c> carried by an opcode (e.g.
/// <c>UnifyConstant.Value</c>, <c>BodySetConst.Value</c>). The whitelist:
/// <list type="bullet">
/// <item>0x00 null</item>
/// <item>0x01 bool</item>
/// <item>0x02 int64 (<c>long</c>)</item>
/// <item>0x03 double</item>
/// <item>0x04 string (UTF-8)</item>
/// <item>0x05 <c>Rt.ConstTerm</c> — wraps one whitelisted primitive</item>
/// <item>0x06 <c>Rt.StructTerm</c> — functor + arity + recurse over args, arbitrary depth (FR-005)</item>
/// </list>
/// Any value whose runtime type is not in this set → <see cref="IlCodecException"/>
/// (FR-006 / SC-004): a loud failure, never a silent drop. A non-ground
/// <c>StructTerm</c> arg (e.g. a <c>VarRef</c>) likewise fails loud — the codec
/// covers ground constants only.
/// </summary>
internal static class ConstantCodec
{
    private const byte TagNull = 0x00;
    private const byte TagBool = 0x01;
    private const byte TagInt64 = 0x02;
    private const byte TagDouble = 0x03;
    private const byte TagString = 0x04;
    private const byte TagConstTerm = 0x05;
    private const byte TagStructTerm = 0x06;

    public static void Write(BinaryWriter w, object? value)
    {
        switch (value)
        {
            case null:
                w.Write(TagNull);
                break;
            case bool b:
                w.Write(TagBool);
                w.Write(b);
                break;
            case long l:
                w.Write(TagInt64);
                w.Write(l);
                break;
            case double d:
                w.Write(TagDouble);
                ByteIo.WriteDouble(w, d);
                break;
            case string s:
                w.Write(TagString);
                ByteIo.WriteString(w, s);
                break;
            case Rt.ConstTerm ct:
                w.Write(TagConstTerm);
                Write(w, ct.Value);
                break;
            case Rt.StructTerm st:
                w.Write(TagStructTerm);
                ByteIo.WriteString(w, st.Functor);
                ByteIo.WriteVarUInt(w, (ulong)st.Args.Count);
                foreach (var arg in st.Args)
                    Write(w, arg);
                break;
            default:
                throw new IlCodecException(
                    $"Out-of-whitelist constant value of runtime type {value.GetType().FullName}");
        }
    }

    public static object? Read(BinaryReader r)
    {
        byte tag = r.ReadByte();
        switch (tag)
        {
            case TagNull:
                return null;
            case TagBool:
                return r.ReadBoolean();
            case TagInt64:
                return r.ReadInt64();
            case TagDouble:
                return ByteIo.ReadDouble(r);
            case TagString:
                return ByteIo.ReadString(r);
            case TagConstTerm:
                return new Rt.ConstTerm(Read(r));
            case TagStructTerm:
            {
                string functor = ByteIo.ReadString(r);
                int arity = checked((int)ByteIo.ReadVarUInt(r));
                var args = new List<Rt.Term>(arity);
                for (int i = 0; i < arity; i++)
                {
                    object? sub = Read(r);
                    if (sub is not Rt.Term t)
                        throw new IlCodecException(
                            "Corrupt payload: StructTerm argument did not decode to a Term");
                    args.Add(t);
                }
                return new Rt.StructTerm(functor, args);
            }
            default:
                throw new IlCodecException($"Unknown constant ctag 0x{tag:X2}");
        }
    }
}
