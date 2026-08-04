// T023/T024 — the contract-rule-5 build assertion: "the engine host's execute
// path MUST NOT reference the compiler assembly; the client gains the compiler
// reference (project-file assertion + build check)."
//
// REALITY OF THE DEPENDENCY KNOT (documented, not forced): the compiler is not
// a separate assembly — glp_runtime_net is monolithic and its RUNTIME sources
// themselves reference GlpRuntime.Compiler (lib/runtime/module_hierarchy.cs,
// lib/engine/glp_engine.cs, lib/multiagent/agent_runtime.cs), and the text
// kinds keep the full pipeline engine-side during the deprecation window
// (rule 3). An assembly-level absence assertion is therefore impossible
// without splitting glp_runtime_net. The enforced boundary is the TYPE that
// IS the IL execute path — GlpRuntime.EngineHost.IlExecutePath — whose
// compiled method bodies (incl. async state machines) are walked here and
// asserted to reference NOTHING in the compiler/type-checker/engine-front-end
// namespaces. Plus the project-file assertions: the CLIENT project now owns
// the compiler reference.

using System.Reflection;
using System.Reflection.Emit;

using GlpRuntime.EngineHost;

namespace GlpRuntime.SplitProtocol.Tests;

public class CompilerAbsenceTests
{
    /// <summary>Namespaces the IL execute path must never touch: the compiler,
    /// the type checker, the compiler-fronted engine facade, and madGLP.</summary>
    private static readonly string[] ForbiddenNamespaces =
    {
        "GlpRuntime.Compiler",
        "GlpRuntime.Analysis",
        "GlpRuntime.Engine",     // GlpEngine's load/run paths parse via the compiler
        "GlpRuntime.Multiagent",
    };

    [Fact]
    public void IlExecutePath_MethodBodies_NeverTouchTheCompiler()
    {
        var offenders = new List<string>();
        foreach (var type in TypeClosure(typeof(IlExecutePath)))
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.Static |
                                     BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(all).Cast<MethodBase>()
                         .Concat(type.GetConstructors(all)))
            {
                foreach (var referenced in ReferencedMembers(method))
                {
                    var ns = (referenced as Type ?? referenced.DeclaringType)?.Namespace ?? "";
                    if (ForbiddenNamespaces.Any(f =>
                            ns == f || ns.StartsWith(f + ".", StringComparison.Ordinal)))
                    {
                        offenders.Add($"{type.Name}.{method.Name} → {ns}.{referenced.Name}");
                    }
                }
            }
        }
        Assert.True(offenders.Count == 0,
            "IL execute path touches forbidden (compiler-side) members:\n  " +
            string.Join("\n  ", offenders.Distinct()));
    }

    [Fact]
    public void IlExecutePath_Signatures_CarryNoCompilerTypes()
    {
        foreach (var type in TypeClosure(typeof(IlExecutePath)))
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.Static |
                                     BindingFlags.DeclaredOnly;
            foreach (var field in type.GetFields(all))
                AssertAllowed(field.FieldType, $"{type.Name}.{field.Name}");
            foreach (var method in type.GetMethods(all))
            {
                AssertAllowed(method.ReturnType, $"{type.Name}.{method.Name} return");
                foreach (var p in method.GetParameters())
                    AssertAllowed(p.ParameterType, $"{type.Name}.{method.Name}({p.Name})");
            }
        }
    }

    [Fact]
    public void ClientProject_GainsTheCompilerReference()
    {
        // Rule 5's other half: the compiler reference moved INTO the client.
        var clientCsproj = File.ReadAllText(
            TestRepo.Csharp(Path.Combine("glp_repl_client", "GlpReplClient.csproj")));
        Assert.Contains("glp_runtime_net.csproj", clientCsproj);  // the compiler-bearing assembly
        Assert.Contains("GlpIlCodec.csproj", clientCsproj);       // the 062 envelope codec
    }

    [Fact]
    public void SplitProtocol_StaysEnvelopeAgnostic()
    {
        // The wire layer carries the envelope OPAQUELY: glp_split_protocol must
        // reference neither the IL codec nor the runtime/compiler assembly.
        var references = File.ReadAllLines(
                TestRepo.Csharp(Path.Combine("glp_split_protocol", "GlpSplitProtocol.csproj")))
            .Where(l => l.Contains("<ProjectReference", StringComparison.Ordinal))
            .ToList();
        Assert.DoesNotContain(references, r => r.Contains("GlpIlCodec"));
        Assert.DoesNotContain(references, r => r.Contains("glp_runtime_net"));
    }

    [Fact]
    public void EngineHost_IlPath_DeclaresTheIlCodecReference()
    {
        var csproj = File.ReadAllText(
            TestRepo.Csharp(Path.Combine("glp_engine_host", "GlpEngineHost.csproj")));
        Assert.Contains("GlpIlCodec.csproj", csproj);
    }

    // ---- reflection plumbing ----

    private static void AssertAllowed(Type t, string where)
    {
        var ns = (t.IsByRef || t.IsArray ? t.GetElementType()! : t).Namespace ?? "";
        Assert.False(ForbiddenNamespaces.Any(f =>
                ns == f || ns.StartsWith(f + ".", StringComparison.Ordinal)),
            $"{where} exposes forbidden type {t.FullName}");
    }

    /// <summary>The type plus every nested type (async/iterator state machines,
    /// closures) — the compiled bodies of the whole IL execute path.</summary>
    private static IEnumerable<Type> TypeClosure(Type root)
    {
        yield return root;
        foreach (var nested in root.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            foreach (var t in TypeClosure(nested))
                yield return t;
    }

    /// <summary>Walk a method body's IL and resolve every member token it references.</summary>
    private static IEnumerable<MemberInfo> ReferencedMembers(MethodBase method)
    {
        var body = method.GetMethodBody();
        if (body is null)
            yield break;
        var il = body.GetILAsByteArray();
        if (il is null)
            yield break;

        var module = method.Module;
        var typeArgs = method.DeclaringType?.IsGenericType == true
            ? method.DeclaringType.GetGenericArguments() : Type.EmptyTypes;
        var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : Type.EmptyTypes;

        int pos = 0;
        while (pos < il.Length)
        {
            var opcode = ReadOpCode(il, ref pos);
            switch (opcode.OperandType)
            {
                case OperandType.InlineMethod:
                case OperandType.InlineField:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                {
                    int token = BitConverter.ToInt32(il, pos);
                    pos += 4;
                    MemberInfo? member = null;
                    try { member = module.ResolveMember(token, typeArgs, methodArgs); }
                    catch (ArgumentException) { /* tokens outside this context — skip */ }
                    if (member is not null)
                        yield return member;
                    break;
                }
                case OperandType.InlineString:
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.ShortInlineR:
                case OperandType.InlineSig:
                    pos += 4;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    pos += 8;
                    break;
                case OperandType.InlineVar:
                    pos += 2;
                    break;
                case OperandType.ShortInlineVar:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineBrTarget:
                    pos += 1;
                    break;
                case OperandType.InlineSwitch:
                {
                    int count = BitConverter.ToInt32(il, pos);
                    pos += 4 + count * 4;
                    break;
                }
                case OperandType.InlineNone:
                    break;
                default:
                    throw new InvalidOperationException(
                        $"unhandled operand type {opcode.OperandType} in {method.Name}");
            }
        }
    }

    private static readonly Dictionary<short, OpCode> OpCodeMap = BuildOpCodeMap();

    private static Dictionary<short, OpCode> BuildOpCodeMap()
    {
        var map = new Dictionary<short, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is OpCode op)
                map[op.Value] = op;
        }
        return map;
    }

    private static OpCode ReadOpCode(byte[] il, ref int pos)
    {
        short value = il[pos++];
        if (value == 0xFE)
            value = (short)(0xFE00 | il[pos++]);
        if (!OpCodeMap.TryGetValue(value, out var opcode))
            throw new InvalidOperationException($"unknown IL opcode 0x{value:X4}");
        return opcode;
    }
}
