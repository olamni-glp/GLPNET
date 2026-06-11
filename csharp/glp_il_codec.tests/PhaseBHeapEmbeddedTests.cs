using GlpRuntime.Engine;
using GlpRuntime.Runtime;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// FR-009b (phase b, accepted-risk increment) — round-trip soundness extends to a
/// <c>ModuleTerm</c>-embedded program reached as heap data. A module WITH exports
/// auto-activates on load, which stores a <c>ModuleTerm</c> (its <c>.Bytecode</c> a
/// real <see cref="GlpRuntime.Bytecode.BytecodeProgram"/>) on the engine heap (T033).
/// <see cref="HeapWalk"/> locates it (T024); the embedded program round-trips
/// structurally and executes equivalently (T025). Bounded to embedded programs — not
/// #7's snapshot envelope.
/// </summary>
[Trait("Category", "PhaseB")]
public class PhaseBHeapEmbeddedTests
{
    // Exports → auto-activation (a ModuleTerm lands on the heap). A nullary main rides
    // along so the embedded program is executable for the execute-equivalence check.
    private const string ModuleSource =
        "-module(m).\n\n" +
        "exported procedure go(Constant?).\n" +
        "go(X) :- ground(X?) | true.\n\n" +
        "procedure main.\n" +
        "main.\n";

    private static GlpEngine ActivatedEngine()
    {
        var engine = new GlpEngine(RepoPaths.RootSelfGlp) { StrictTypes = false };
        engine.LoadSource(ModuleSource, filename: "m");
        return engine;
    }

    [Fact]
    public void Embedded_module_program_is_located_on_the_heap_and_round_trips()
    {
        var engine = ActivatedEngine();

        var embedded = HeapWalk.FindEmbeddedPrograms(engine.Runtime.Heap);
        Assert.NotEmpty(embedded); // T033: a real ModuleTerm is reachable

        foreach (var e in embedded)
        {
            var decoded = IlCodec.Decode(IlCodec.Encode(e.Program)).Program;
            ProgramEquality.AssertStructurallyEqual(e.Program, decoded);
        }
    }

    [Fact]
    public async Task Embedded_program_executes_equivalently()
    {
        var engine = ActivatedEngine();
        var e = HeapWalk.FindEmbeddedPrograms(engine.Runtime.Heap)[0];

        var decoded = IlCodec.Decode(IlCodec.Encode(e.Program)).Program;
        var original = await GlpExecutor.RunNullaryAsync(e.Program, "main/0");
        var roundTripped = await GlpExecutor.RunNullaryAsync(decoded, "main/0");

        Assert.Equal(original, roundTripped);
        Assert.Equal(ExecutionStatus.Succeeded, original);
    }

    [Fact]
    public void IlHeapCodec_round_trips_every_embedded_program()
    {
        var engine = ActivatedEngine();

        var results = IlHeapCodec.RoundTripEmbedded(engine.Runtime.Heap);
        Assert.NotEmpty(results);

        var located = HeapWalk.FindEmbeddedPrograms(engine.Runtime.Heap);
        foreach (var r in results)
        {
            var src = located.First(x => x.Address == r.Address);
            ProgramEquality.AssertStructurallyEqual(src.Program, r.Decoded);
        }
    }
}
