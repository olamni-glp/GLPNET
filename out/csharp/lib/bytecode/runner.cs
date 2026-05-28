// STUB — bulk codegen deferred.
// Full ~4863-line WAM/FCP interpreter conversion is tracked at:
//   .codeconv/conversion-code/lib/bytecode/runner.dart.md
// source: glp_runtime/lib/bytecode/runner.dart

using System;
using System.Collections.Generic;
using GlpRuntime.Runtime;

namespace GlpRuntime.Bytecode;

internal static class _RunnerStub
{
    public const string Deferred =
        "Runner.dart bulk codegen deferred — see .codeconv/conversion-code/lib/bytecode/runner.dart.md";
}

// ── Enums ──────────────────────────────────────────────────────────────────

public enum RunResult { Terminated, Suspended, Yielded, OutOfReductions }

public enum UnifyMode { Read, Write }

public enum GuardResult { Pass, Fail, Suspend }

// ── BytecodeProgram ────────────────────────────────────────────────────────

/// <summary>
/// Compiled GLP bytecode program.
/// Holds the instruction list and a label→PC index.
/// </summary>
public sealed class BytecodeProgram
{
    /// <summary>Label→entry-PC map (e.g. "serve/2" → PC).</summary>
    public Dictionary<string, int> Labels { get; } = new();

    /// <summary>Raw instruction list (Op objects from opcodes.cs).</summary>
    public IReadOnlyList<object> Instructions { get; } = Array.Empty<object>();

    public BytecodeProgram(IReadOnlyList<object> instructions)
        => throw new NotImplementedException(_RunnerStub.Deferred);

    public BytecodeProgram() { }
}

// ── CallEnv ────────────────────────────────────────────────────────────────

/// <summary>
/// Per-goal argument environment: maps argument slot index to Term.
/// </summary>
public sealed class CallEnv
{
    public IReadOnlyDictionary<int, Term> Args { get; } = new Dictionary<int, Term>();

    public CallEnv(Dictionary<int, Term> args)
        => throw new NotImplementedException(_RunnerStub.Deferred);

    public CallEnv() { }
}

// ── EnvironmentFrame ───────────────────────────────────────────────────────

/// <summary>WAM-style environment frame (activation record).</summary>
public sealed class EnvironmentFrame
{
    public int ReturnPc { get; } = default;
    public IReadOnlyList<Term> PermanentVars { get; } = Array.Empty<Term>();

    public EnvironmentFrame(int returnPc, IReadOnlyList<Term> permanentVars)
        => throw new NotImplementedException(_RunnerStub.Deferred);
}

// ── RunnerContext ──────────────────────────────────────────────────────────

/// <summary>
/// Per-goal execution context passed to each RunStep call.
/// </summary>
public sealed class RunnerContext
{
    public GlpRuntimeEngine Rt { get; } = default!;
    public int GoalId { get; } = default;
    public int Pc { get; set; } = default;

    public RunnerContext(GlpRuntimeEngine rt, int goalId, int pc)
        => throw new NotImplementedException(_RunnerStub.Deferred);
}

// ── ReplModuleTarget / ReplModuleContext ───────────────────────────────────

/// <summary>Module import target held by the REPL module context.</summary>
public sealed class ReplModuleTarget
{
    public string Name { get; } = string.Empty;
    public BytecodeProgram Program { get; } = default!;

    public ReplModuleTarget(string name, BytecodeProgram program)
        => throw new NotImplementedException(_RunnerStub.Deferred);
}

/// <summary>Simple module context used by the REPL for synchronous goal spawning.</summary>
public sealed class ReplModuleContext
{
    public string ModuleName { get; } = string.Empty;
    public IReadOnlyDictionary<int, ReplModuleTarget> Imports { get; } =
        new Dictionary<int, ReplModuleTarget>();
    public BytecodeProgram? CombinedProgram { get; } = null;
    public string ProgramKey { get; } = string.Empty;

    public ReplModuleContext(
        string moduleName,
        Dictionary<int, ReplModuleTarget> imports,
        BytecodeProgram? combinedProgram,
        string programKey)
        => throw new NotImplementedException(_RunnerStub.Deferred);
}

// ── BytecodeRunner ─────────────────────────────────────────────────────────

/// <summary>
/// WAM/FCP bytecode interpreter.
/// Full implementation deferred — see escalation note at the top of this file.
/// </summary>
public sealed class BytecodeRunner
{
    public BytecodeProgram Program { get; } = default!;

    public BytecodeRunner(BytecodeProgram program)
        => throw new NotImplementedException(_RunnerStub.Deferred);

    /// <summary>Execute one scheduling quantum for the given goal.</summary>
    public RunResult RunStep(RunnerContext cx, CallEnv env, int reductions)
        => throw new NotImplementedException(_RunnerStub.Deferred);
}
