using System.Collections;
using System.Reflection;
using GlpRuntime.Bytecode;
using Rt = GlpRuntime.Runtime;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// Structural identity for decoded programs (FR-002). Compares opcode OBJECTS — not
/// the codec's own bytes — so the gate is independent of the encoder: same family
/// (runtime type), same operands incl. <c>IsReader</c> polarity, constants compared
/// recursively (Term-aware), same order, and an equal recomputed <c>Labels</c> map.
/// </summary>
internal static class ProgramEquality
{
    public static void AssertStructurallyEqual(BytecodeProgram expected, BytecodeProgram actual)
    {
        Assert.Equal(expected.Instructions.Count, actual.Instructions.Count);
        for (int i = 0; i < expected.Instructions.Count; i++)
        {
            var a = expected.Instructions[i];
            var b = actual.Instructions[i];
            Assert.True(InstructionEqual(a, b),
                $"Instruction {i} differs: {Describe(a)} vs {Describe(b)}");
        }

        // Labels are recomputed on decode (D2): identical instructions ⇒ identical map.
        Assert.Equal(expected.Labels.Count, actual.Labels.Count);
        foreach (var kv in expected.Labels)
        {
            Assert.True(actual.Labels.TryGetValue(kv.Key, out var pc) && pc == kv.Value,
                $"Label '{kv.Key}' differs: expected PC {kv.Value}");
        }
    }

    public static bool InstructionEqual(object? a, object? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a.GetType() != b.GetType()) return false;

        foreach (var p in a.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length != 0 || !p.CanRead) continue;
            if (!ValueEqual(p.GetValue(a), p.GetValue(b))) return false;
        }
        return true;
    }

    private static bool ValueEqual(object? a, object? b)
    {
        if (a is null || b is null) return a is null && b is null;

        if (a is Rt.Term ta && b is Rt.Term tb) return TermEqual(ta, tb);

        // Operand lists (e.g. BodySetStructConstArgs.ConstArgs) — element-wise.
        if (a is IList la && b is IList lb && a is not string)
        {
            if (la.Count != lb.Count) return false;
            for (int i = 0; i < la.Count; i++)
                if (!ValueEqual(la[i], lb[i])) return false;
            return true;
        }

        return Equals(a, b);
    }

    private static bool TermEqual(Rt.Term a, Rt.Term b)
    {
        switch (a, b)
        {
            case (Rt.ConstTerm ca, Rt.ConstTerm cb):
                return ValueEqual(ca.Value, cb.Value);
            case (Rt.StructTerm sa, Rt.StructTerm sb):
                if (sa.Functor != sb.Functor || sa.Args.Count != sb.Args.Count) return false;
                for (int i = 0; i < sa.Args.Count; i++)
                    if (!TermEqual(sa.Args[i], sb.Args[i])) return false;
                return true;
            default:
                return Equals(a, b);
        }
    }

    private static string Describe(object? o) => o?.GetType().Name ?? "<null>";
}
