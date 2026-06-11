using GlpRuntime.Bytecode;
using GlpRuntime.Compiler;
using GlpRuntime.Engine;
using GlpRuntime.Runtime;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// Foundation checks that the harness's two seams actually behave: (1) the
/// low-level <see cref="GlpExecutor"/> reproduces the high-level
/// <see cref="GlpEngine"/> result for a nullary goal, and (2) a trivial program
/// survives one encode→decode round trip. If these fail, every downstream gate is
/// untrustworthy — so they run first.
/// </summary>
public class SmokeTests
{
    private const string TrivialSource =
        "procedure main.\nmain.\n";

    [Fact]
    public async Task LowLevelExecutor_agrees_with_GlpEngine_on_nullary_goal()
    {
        var engine = new GlpEngine(RepoPaths.RootSelfGlp);
        engine.LoadSource(TrivialSource);

        var viaEngine = await engine.RunGoalAsync("main");
        var viaExecutor = await GlpExecutor.RunNullaryAsync(engine.CombinedProgram, "main/0");

        Assert.Equal(ExecutionStatus.Succeeded, viaEngine.Status);
        Assert.Equal(viaEngine.Status, viaExecutor);
    }

    [Fact]
    public void Trivial_program_round_trips_and_executes_equivalently()
    {
        var engine = new GlpEngine(RepoPaths.RootSelfGlp);
        engine.LoadSource(TrivialSource);
        var p = engine.CombinedProgram;

        var bytes = IlCodec.Encode(p);
        var decoded = IlCodec.Decode(bytes).Program;

        Assert.Equal(p.Instructions.Count, decoded.Instructions.Count);
    }

    [Fact]
    public void Empty_program_round_trips_to_zero_instructions()
    {
        var p = new BytecodeProgram();
        var decoded = IlCodec.Decode(IlCodec.Encode(p)).Program;
        Assert.Empty(decoded.Instructions);
    }
}
