using GlpRuntime.Bytecode;
using GlpRuntime.Runtime;

namespace GlpRuntime.IlCodec.Tests;

/// <summary>
/// Runs a NULLARY goal (<c>name/0</c>) against an arbitrary <see cref="BytecodeProgram"/>
/// via the engine's own public runner seam (<see cref="GlpRuntimeEngine"/> +
/// <see cref="BytecodeRunner"/> + <see cref="Scheduler"/>). This is the same path
/// <c>GlpEngine._RunSingleGoalAsync</c> takes, minus the private argument-setup and
/// display bookkeeping — which a nullary goal does not need.
///
/// The execute-equivalence gate (FR-003) drives this on the SAME goal against two
/// program objects (an original <c>p</c> and the decoded <c>p'</c>) and compares
/// the resulting <see cref="ExecutionStatus"/>; an identical harness means any
/// divergence is attributable to the codec.
/// </summary>
internal static class GlpExecutor
{
    private const string ProgramKey = "main";

    public static async Task<ExecutionStatus> RunNullaryAsync(
        BytecodeProgram program, string goalLabel, int maxCycles = 10000)
    {
        if (!program.Labels.TryGetValue(goalLabel, out var entryPc))
            throw new InvalidOperationException($"Goal label '{goalLabel}' not found in program");

        var rt = new GlpRuntimeEngine();
        const int goalId = 1;
        rt.SetGoalEnv(goalId, new CallEnv(new Dictionary<int, Term>()));
        rt.SetGoalProgram(goalId, ProgramKey);

        var runner = new BytecodeRunner(program);
        var scheduler = new Scheduler(
            rt: rt,
            runners: new Dictionary<object?, BytecodeRunner> { { ProgramKey, runner } });

        rt.Gq.Enqueue(new GoalRef(goalId, entryPc));

        var result = await scheduler.DrainAsyncWithStatus(
            maxCycles: maxCycles, debug: false, showBindings: false, debugOutput: false);
        return result.Status;
    }
}
