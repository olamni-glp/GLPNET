using GlpRuntime.Bytecode;
using GlpRuntime.Compiler;
using GlpRuntime.Engine;
using GlpRuntime.Runtime;
using Bc = GlpRuntime.Bytecode;
using V2 = GlpRuntime.Bytecode.V2;
using Rt = GlpRuntime.Runtime;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// One verification-corpus entry. <see cref="Program"/> is always round-tripped by
/// the structural-identity gate (FR-002). When <see cref="Goal"/> is non-null the
/// program is also executable: the execute-equivalence gate (FR-003) runs that
/// nullary goal against the program and its decoded twin and compares status —
/// against <see cref="ExpectedStatus"/> as a sanity anchor. The empty program is
/// execute-exempt (Goal == null), covered by structural identity only (F2).
/// </summary>
internal sealed record CorpusCase(
    string Name,
    BytecodeProgram Program,
    IReadOnlyDictionary<string, long>? VariableMap,
    string? Goal,
    ExecutionStatus? ExpectedStatus);

/// <summary>
/// The ≥10-program verification corpus (FR-007 / data-model corpus table). Three
/// programs are compiled from GLP source and run (succeed / fail / suspend); the
/// rest are concrete <see cref="BytecodeProgram"/> objects that pin the opcode/
/// constant edge cases real codegen does not readily emit (every opcode class, the
/// obsolete opcodes A3, every constant ctag incl. deep recursion, v2-only, mixed).
/// The ≥10 floor (F3) is met by construction — 10 named entries below.
/// </summary>
internal static class Corpus
{
    // ── Compiled, runnable nullary sources ───────────────────────────────────

    private const string SucceedSource =
        "procedure main.\nmain.\n";

    private const string FailSource =
        "procedure main.\nmain :- 1 =?= 2 | true.\n";

    // main spawns a reader (await) on a stream whose writer (held by hold/1) is
    // never bound → await suspends on the unbound reader → the goal suspends.
    private const string SuspendSource =
        "procedure main.\n" +
        "main :- await(S?), hold(S).\n" +
        "\n" +
        "procedure await(Stream(Constant)?).\n" +
        "await([V|In]) :- ground(V?) | await(In?).\n" +
        "await([]).\n" +
        "\n" +
        "procedure hold(Stream(Constant)).\n" +
        "hold(_).\n";

    private static IReadOnlyList<CorpusCase>? _cache;

    public static IReadOnlyList<CorpusCase> All() => _cache ??= Build();

    public static CorpusCase ByName(string name) => All().First(c => c.Name == name);

    private static IReadOnlyList<CorpusCase> Build()
    {
        var cases = new List<CorpusCase>
        {
            Compiled("succeed", SucceedSource, ExecutionStatus.Succeeded),
            Compiled("fail", FailSource, ExecutionStatus.Failed),
            Compiled("suspend", SuspendSource, ExecutionStatus.Suspended),
            Synthetic("all_opcodes", OpcodeSamples.AllConcreteOpcodes()),
            Synthetic("obsolete", Obsolete()),
            Synthetic("const_sweep", ConstSweep(), HandVariableMap()),
            Synthetic("v2_only", V2Only()),
            Synthetic("mixed_v1v2", MixedV1V2()),
            Synthetic("recursive_constant", RecursiveConstant()),
            Synthetic("empty", new BytecodeProgram()),
        };
        return cases;
    }

    public static IEnumerable<CorpusCase> Runnable() => All().Where(c => c.Goal is not null);

    // ── Builders ─────────────────────────────────────────────────────────────

    private static CorpusCase Compiled(string name, string source, ExecutionStatus expected)
    {
        // StrictTypes off: the codec round-trips whatever bytecode the compiler emits;
        // GLP type-correctness of the fixture is not the spike's concern (the suspend
        // fixture deliberately uses loose stream types). Parse errors still throw.
        var engine = new GlpEngine(RepoPaths.RootSelfGlp) { StrictTypes = false };
        engine.LoadSource(source);
        var exec = engine.CombinedProgram;
        var meta = new GlpCompiler().CompileWithMetadata(source);
        return new CorpusCase(name, exec, meta.VariableMap, "main/0", expected);
    }

    private static CorpusCase Synthetic(string name, IReadOnlyList<object> ops,
        IReadOnlyDictionary<string, long>? variableMap = null) =>
        new(name, new BytecodeProgram(ops.ToList()), variableMap, null, null);

    private static CorpusCase Synthetic(string name, BytecodeProgram program) =>
        new(name, program, null, null, null);

#pragma warning disable CS0612 // the obsolete opcodes are deliberately round-tripped (A3)
    private static List<object> Obsolete() => new()
    {
        new Bc.Label("p/0"),
        new Bc.UnionSiAndGoto("p/0"),
        new Bc.ResetAndGoto("p/0"),
        new Bc.Commit(),
    };
#pragma warning restore CS0612

    // Every constant ctag 0x00–0x06, including a deeply nested StructTerm (FR-005).
    private static List<object> ConstSweep() => new()
    {
        new Bc.UnifyConstant(null),                                   // 0x00 null
        new Bc.UnifyConstant(true),                                   // 0x01 bool
        new Bc.UnifyConstant(42L),                                    // 0x02 int64
        new Bc.UnifyConstant(3.14159),                                // 0x03 double
        new Bc.UnifyConstant("atom"),                                 // 0x04 string
        new Bc.UnifyConstant(new Rt.ConstTerm("wrapped")),            // 0x05 ConstTerm
        new Bc.UnifyConstant(DeepList()),                             // 0x06 StructTerm (recursive)
    };

    // [1, 2.0, "three"] as a runtime cons structure: ./2(1, ./2(2.0, ./2("three", nil))).
    private static Rt.StructTerm DeepList() =>
        new(".", new List<Rt.Term>
        {
            new Rt.ConstTerm(1L),
            new Rt.StructTerm(".", new List<Rt.Term>
            {
                new Rt.ConstTerm(2.0),
                new Rt.StructTerm(".", new List<Rt.Term>
                {
                    new Rt.ConstTerm("three"),
                    new Rt.ConstTerm("nil"),
                }),
            }),
        });

    private static List<object> RecursiveConstant() => new()
    {
        new Bc.UnifyConstant(DeepList()),
        new Bc.BodySetStructConstArgs(0, "wrap", new List<object?> { DeepList(), 99L }),
    };

    private static List<object> V2Only() => new()
    {
        new V2.HeadVariable(0, isReader: false),
        new V2.GetVariable(1, 0, isReader: true),
        new V2.UnifyVariable(2, isReader: false),
        new V2.PutVariable(3, 1, isReader: true),
        new V2.SetVariable(4, isReader: false),
        new V2.Unknown(5),
    };

    private static List<object> MixedV1V2() => new()
    {
        new Bc.Label("q/1"),
        new Bc.HeadConstant(7L, 0),
        new V2.GetVariable(0, 0, isReader: false),
        new Bc.Commit(),
        new V2.PutVariable(0, 0, isReader: true),
        new Bc.Proceed(),
    };

    private static Dictionary<string, long> HandVariableMap() => new()
    {
        { "X", 0 },
        { "Result", 3 },
        { "Acc", 7 },
    };
}
