using Bc = GlpRuntime.Bytecode;
using V2 = GlpRuntime.Bytecode.V2;

namespace GlpRuntime.IlCodec;

/// <summary>The result of <see cref="IlCodec.Decode"/>: the rebuilt program plus its optional per-module variable map.</summary>
public sealed record DecodedProgram(Bc.BytecodeProgram Program, IReadOnlyDictionary<string, long>? VariableMap);

/// <summary>
/// The IL/bytecode round-trip codec (FR-001..FR-006): <c>compile → encode → decode</c>
/// reproduces the exact opcode objects — family, <c>IsReader</c> polarity, operands,
/// and order. Equivalence is structural identity; the <c>Labels</c> map is NOT
/// serialized but recomputed by the <see cref="Bc.BytecodeProgram"/> constructor's
/// <c>IndexLabels</c> from the decoded <c>Label</c> markers (research D2). Every
/// failure mode is loud (<see cref="IlCodecException"/>); nothing is silently dropped.
/// </summary>
public static class IlCodec
{
    /// <summary>Encode a compiled program (and optional per-module variable map) to a self-describing byte payload.</summary>
    public static byte[] Encode(Bc.BytecodeProgram program, IReadOnlyDictionary<string, long>? variableMap = null)
    {
        ArgumentNullException.ThrowIfNull(program);

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        PayloadHeader.Write(w);

        var ins = program.Instructions;
        ByteIo.WriteVarUInt(w, (ulong)ins.Count);
        foreach (var op in ins)
            WriteInstruction(w, op);

        if (variableMap is null)
        {
            w.Write((byte)0);
        }
        else
        {
            w.Write((byte)1);
            ByteIo.WriteVarUInt(w, (ulong)variableMap.Count);
            foreach (var kv in variableMap)
            {
                ByteIo.WriteString(w, kv.Key);
                w.Write(kv.Value);
            }
        }

        w.Flush();
        return ms.ToArray();
    }

    /// <summary>Decode a byte payload back to a <see cref="DecodedProgram"/>. Throws <see cref="IlCodecException"/> on any corruption.</summary>
    public static DecodedProgram Decode(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var ms = new MemoryStream(payload, writable: false);
        using var r = new BinaryReader(ms);

        try
        {
            PayloadHeader.Read(r);

            int count = checked((int)ByteIo.ReadVarUInt(r));
            var instructions = new List<object>(count);
            for (int i = 0; i < count; i++)
                instructions.Add(ReadInstruction(r));

            IReadOnlyDictionary<string, long>? variableMap = null;
            byte hasMap = r.ReadByte();
            if (hasMap == 1)
            {
                int n = checked((int)ByteIo.ReadVarUInt(r));
                var map = new Dictionary<string, long>(n);
                for (int i = 0; i < n; i++)
                {
                    string name = ByteIo.ReadString(r);
                    long reg = r.ReadInt64();
                    map[name] = reg;
                }
                variableMap = map;
            }
            else if (hasMap != 0)
            {
                throw new IlCodecException($"Corrupt payload: hasVariableMap flag 0x{hasMap:X2} (expected 0 or 1)");
            }

            // Labels are recomputed by the constructor's IndexLabels (D2) — never serialized.
            return new DecodedProgram(new Bc.BytecodeProgram(instructions), variableMap);
        }
        catch (EndOfStreamException ex)
        {
            throw new IlCodecException("Truncated payload: reached end of stream before the program was fully decoded", ex);
        }
    }

    private static void WriteInstruction(BinaryWriter w, object op)
    {
        if (op is null)
            throw new IlCodecException("Out-of-family instruction: null element in the instruction list");

        if (OpcodeDiscriminant.TryGetByType(op.GetType(), out var entry))
        {
            w.Write(entry.Family);
            w.Write(entry.Disc);
            entry.Write(w, op);
            return;
        }

        // Known family but no discriminant entry (F1) vs genuinely out-of-family (D4).
        if (op is Bc.IOp || op is V2.IOpV2)
            throw new IlCodecException(
                $"No discriminant-table entry for opcode class {op.GetType().FullName} (registry is incomplete — F1)");

        throw new IlCodecException(
            $"Out-of-family instruction: {op.GetType().FullName} is neither IOp, IOpV2, nor Label");
    }

    private static object ReadInstruction(BinaryReader r)
    {
        byte family = r.ReadByte();
        byte disc = r.ReadByte();
        if (OpcodeDiscriminant.TryGetByCode(family, disc, out var entry))
            return entry.Read(r);

        throw new IlCodecException($"Unknown instruction discriminant: family 0x{family:X2}, disc 0x{disc:X2}");
    }
}
