using System.Reflection;
using Bc = GlpRuntime.Bytecode;
using V2 = GlpRuntime.Bytecode.V2;
using Rt = GlpRuntime.Runtime;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// Reflects over the <c>glp_runtime_net</c> assembly to enumerate EVERY concrete
/// opcode class (v1 <c>IOp</c>, v2 <c>IOpV2</c>, and the <c>Label</c> marker) and
/// synthesize one instance of each with placeholder operands. Powers two gates:
/// the coverage program (T021 — a single program that exercises every class) and
/// the discriminant-completeness test (T029 — every type must have a registry
/// entry, independent of any corpus program).
/// </summary>
internal static class OpcodeSamples
{
    /// <summary>Every concrete opcode runtime type in the engine assembly (v1, v2, and Label).</summary>
    public static IReadOnlyList<Type> AllConcreteOpcodeTypes()
    {
        var asm = typeof(Bc.IOp).Assembly;
        return asm.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                        (typeof(Bc.IOp).IsAssignableFrom(t) || typeof(V2.IOpV2).IsAssignableFrom(t)))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>One synthesized instance of every concrete opcode class.</summary>
    public static IReadOnlyList<object> AllConcreteOpcodes() =>
        AllConcreteOpcodeTypes().Select(Construct).ToList();

    public static object Construct(Type t)
    {
        var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var args = ctor.GetParameters().Select(p => SampleArg(p.ParameterType)).ToArray();
        return ctor.Invoke(args)!;
    }

    private static object? SampleArg(Type pt)
    {
        if (pt == typeof(long)) return 1L;
        if (pt == typeof(bool)) return true;
        if (pt == typeof(string)) return "L0";
        if (pt == typeof(object)) return 0L;                      // object? Value — a whitelist primitive
        if (pt == typeof(List<object?>)) return new List<object?> { 0L, "x" };
        if (pt == typeof(IReadOnlyList<Rt.Term>) || pt == typeof(List<Rt.Term>))
            return new List<Rt.Term> { new Rt.ConstTerm(0L) };
        throw new InvalidOperationException(
            $"OpcodeSamples has no placeholder for constructor parameter type {pt.FullName}");
    }
}
