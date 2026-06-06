// WAM/FCP bytecode interpreter — Dart→C# conversion of glp_runtime/lib/bytecode/runner.dart.
// Converted via the escalation-E1 6-chunk split (skeleton + HEAD + UNIFY/v2 + BODY-build
// + clause-control/commit + concurrency/guards/helpers), feature 020, 2026-06-03. COMPLETE:
//   - Support types (BytecodeProgram, CallEnv, EnvironmentFrame, _ParentContext,
//     RunnerContext, _ArgInfo, _TentativeStruct, _ClauseVar, _ListStruct, _StructureState).
//   - Dispatch loop (RunStep → RunWithStatus): pc advance, WAM read/write mode, reduction
//     countdown, terminate/suspend/yield; each opcode arm is a private Exec<OpName> method.
//   - All 60 arms + helpers (_evaluateGuard, _dereferenceWithTracking, _evaluateArithmetic,
//     _termsEqual, _convertTentativeToStruct) implemented; preserves two-phase HEAD/GUARD/BODY
//     (σ̂w/Si/U), writer-MGU, FCP wake-on-binding, tail-call kappa, module-RPC GlpChannel send.
//   - Build-verified (full sln green). Behavioural/trace fidelity is verified by feature-020
//     US1/US2 trace-equivalence (T017/T022), not by the build gate alone.
// source: glp_runtime/lib/bytecode/runner.dart (4863 lines)
// Public signatures are preserved VERBATIM from the prior stub — downstream files
// (scheduler, glp_engine, isolate_manager, agent_runtime, codegen, linter, glp_repl)
// are already built against them.

using System;
using System.Collections.Generic;
using System.Text;
using GlpRuntime.Runtime;
using V2 = GlpRuntime.Bytecode.V2;

namespace GlpRuntime.Bytecode;

// ── Enums ──────────────────────────────────────────────────────────────────

public enum RunResult { Terminated, Suspended, Yielded, OutOfReductions }

public enum UnifyMode { Read, Write }

public enum GuardResult { Pass, Fail, Suspend }

// ── BytecodeProgram ────────────────────────────────────────────────────────

/// <summary>
/// Compiled GLP bytecode program.
/// Holds the instruction list and a label→PC index.
/// The instruction list is heterogeneous: it holds both v1 (IOp) and v2 (IOpV2) opcodes.
/// </summary>
public sealed class BytecodeProgram
{
    /// <summary>Raw instruction list (Op objects from opcodes.cs / opcodes_v2.cs).</summary>
    public IReadOnlyList<object> Instructions { get; }

    /// <summary>Label→entry-PC map (e.g. "serve/2" → PC). First occurrence wins.</summary>
    public Dictionary<string, int> Labels { get; }

    public BytecodeProgram(IReadOnlyList<object> instructions)
    {
        Instructions = instructions ?? Array.Empty<object>();
        Labels = IndexLabels(Instructions);
    }

    public BytecodeProgram()
    {
        Instructions = Array.Empty<object>();
        Labels = new Dictionary<string, int>();
    }

    private static Dictionary<string, int> IndexLabels(IReadOnlyList<object> ops)
    {
        var m = new Dictionary<string, int>();
        for (var i = 0; i < ops.Count; i++)
        {
            // Keep first occurrence of each label (for multi-clause procedures).
            if (ops[i] is Label label && !m.ContainsKey(label.Name))
            {
                m[label.Name] = i;
            }
        }
        return m;
    }

    /// <summary>
    /// Merge another program into this one (prepend stdlib).
    /// Returns a new BytecodeProgram with all ops from both (other first, then this).
    /// </summary>
    public BytecodeProgram Merge(BytecodeProgram other)
    {
        var merged = new List<object>(other.Instructions.Count + Instructions.Count);
        merged.AddRange(other.Instructions);
        merged.AddRange(Instructions);
        return new BytecodeProgram(merged);
    }

    /// <summary>Generate human-readable disassembly of bytecode.</summary>
    public string ToDisassembly()
    {
        var buffer = new StringBuilder();
        for (var i = 0; i < Instructions.Count; i++)
        {
            buffer.AppendLine($"PC {i}: {InstructionToString(Instructions[i])}");
        }
        return buffer.ToString();
    }

    private static string InstructionToString(object op)
    {
        // Handle v2 PutVariable (the critical one for debugging).
        if (op is V2.PutVariable putVar)
        {
            var mode = putVar.IsReader ? "reader" : "writer";
            return $"PutVariable(X{putVar.VarIndex} → A{putVar.ArgSlot}, {mode})";
        }
        if (op is V2.HeadVariable headVar)
        {
            var mode = headVar.IsReader ? "reader" : "writer";
            return $"HeadVariable(X{headVar.VarIndex}, {mode})";
        }
        if (op is V2.UnifyVariable unifyVar)
        {
            var mode = unifyVar.IsReader ? "reader" : "writer";
            return $"UnifyVariable(X{unifyVar.VarIndex}, {mode})";
        }
        if (op is V2.SetVariable setVar)
        {
            var mode = setVar.IsReader ? "reader" : "writer";
            return $"SetVariable(X{setVar.VarIndex}, {mode})";
        }
        return op.ToString() ?? "<null>";
    }
}

// ── CallEnv ────────────────────────────────────────────────────────────────

/// <summary>
/// Goal-call environment: maps arg slots to heterogeneous Terms (VarRef, ConstTerm, StructTerm).
/// Per spec v2.16 section 1.1: argument registers hold Terms, not just variable IDs.
/// </summary>
public sealed class CallEnv
{
    private readonly Dictionary<int, Term> _argBySlot;

    /// <summary>Slot→Term map. Mutable view kept for parity with the Dart field.</summary>
    public IReadOnlyDictionary<int, Term> Args => _argBySlot;

    public CallEnv(Dictionary<int, Term> args)
    {
        _argBySlot = args ?? new Dictionary<int, Term>();
    }

    public CallEnv()
    {
        _argBySlot = new Dictionary<int, Term>();
    }

    /// <summary>Get argument term at slot (A1, A2, ..., An), or null if absent.</summary>
    public Term? Arg(int slot) => _argBySlot.TryGetValue(slot, out var t) ? t : null;

    /// <summary>Update environment with new argument mappings (for requeue/tail calls).</summary>
    public void Update(Dictionary<int, Term> newArgs)
    {
        _argBySlot.Clear();
        foreach (var kv in newArgs) _argBySlot[kv.Key] = kv.Value;
    }
}

// ── EnvironmentFrame ───────────────────────────────────────────────────────

/// <summary>
/// Environment frame for permanent variables (Y registers).
/// Used by non-tail-recursive predicates to save local state across procedure calls.
/// </summary>
public sealed class EnvironmentFrame
{
    /// <summary>Previous environment (E register).</summary>
    public EnvironmentFrame? Parent { get; }

    /// <summary>Return address (CP register).</summary>
    public int ContinuationPointer { get; }

    /// <summary>Y1, Y2, ..., Yn permanent variables.</summary>
    public List<object?> PermanentVars { get; }

    // Stub-compatible read-only views (public surface preserved from prior stub).
    public int ReturnPc => ContinuationPointer;

    /// <summary>Full constructor mirroring the Dart named-arg form.</summary>
    public EnvironmentFrame(EnvironmentFrame? parent, int continuationPointer, int size)
    {
        Parent = parent;
        ContinuationPointer = continuationPointer;
        PermanentVars = new List<object?>(new object?[size]);
    }

    /// <summary>
    /// Stub-signature constructor (returnPc, permanentVars) preserved VERBATIM.
    /// Builds a parentless frame whose permanent vars are seeded from the supplied list.
    /// </summary>
    public EnvironmentFrame(int returnPc, IReadOnlyList<Term> permanentVars)
    {
        Parent = null;
        ContinuationPointer = returnPc;
        PermanentVars = new List<object?>(permanentVars.Count);
        foreach (var v in permanentVars) PermanentVars.Add(v);
    }

    /// <summary>Get permanent variable Yi (1-indexed).</summary>
    public object? GetY(int index) => PermanentVars[index - 1];

    /// <summary>Set permanent variable Yi (1-indexed).</summary>
    public void SetY(int index, object? value) => PermanentVars[index - 1] = value;
}

// ── _ParentContext ─────────────────────────────────────────────────────────

/// <summary>Parent context for nested structure building.</summary>
public sealed class _ParentContext
{
    public object? Structure { get; }
    public int S { get; }
    public UnifyMode Mode { get; }
    public object? WriterId { get; }

    public _ParentContext(object? structure, int s, UnifyMode mode, object? writerId)
    {
        Structure = structure;
        S = s;
        Mode = mode;
        WriterId = writerId;
    }
}

// ── RunnerContext ──────────────────────────────────────────────────────────

/// <summary>
/// Per-goal execution context. Carries σ̂w (tentative writer bindings), the suspension
/// sets Si/U, clause-variable bindings, the WAM-style structure-traversal state, the
/// argument registers, environment frames, and trace hooks.
/// </summary>
public sealed class RunnerContext
{
    // ── Public surface preserved VERBATIM from the prior stub ──
    public GlpRuntimeEngine Rt { get; }
    public int GoalId { get; }
    public int Pc { get; set; }

    // ── Rich Dart fields (translated faithfully) ──

    /// <summary>Entry point / current procedure label PC. Mutable — updated by Requeue for tail calls.</summary>
    public int Kappa { get; set; }

    public CallEnv Env { get; private set; }

    /// <summary>σ̂w: tentative writer bindings (writer addr → value).</summary>
    public Dictionary<int, object?> SigmaHat { get; } = new();

    /// <summary>Clause-level preliminary suspension set.</summary>
    public HashSet<int> Si { get; } = new();

    /// <summary>Goal-level suspension set (reader IDs).</summary>
    public HashSet<int> U { get; } = new();

    public bool InBody { get; set; }

    // WAM-style structure traversal state
    public UnifyMode Mode { get; set; } = UnifyMode.Read;
    public int S { get; set; }
    public object? CurrentStructure { get; set; }

    /// <summary>Clause variable bindings (varIndex → value).</summary>
    public Dictionary<int, object?> ClauseVars { get; } = new();

    /// <summary>Parent structure stack for nested structure building (arbitrary depth).</summary>
    public List<_ParentContext> ParentStack { get; } = new();

    /// <summary>Argument registers for goal calls (A1..An) — heterogeneous term storage.</summary>
    public Dictionary<int, Term> ArgSlots { get; } = new();

    /// <summary>Target argSlot when building structure for a guard argument.</summary>
    public int? GuardArgSlot { get; set; }

    /// <summary>Reduction budget (null = unlimited).</summary>
    public int? ReductionBudget { get; set; }
    public int ReductionsUsed { get; set; }

    // Environment frames for permanent variables (Y registers)
    public EnvironmentFrame? E { get; set; }
    public int? CP { get; set; }

    /// <summary>Host log hook fired on activation.</summary>
    public Action<GoalRef>? OnActivation { get; set; }

    /// <summary>Track spawned goals for display.</summary>
    public List<string> SpawnedGoals { get; } = new();

    /// <summary>Formatted head goal for trace (mutable for tail calls).</summary>
    public string? GoalHead { get; set; }

    /// <summary>Procedure name for delayed head formatting.</summary>
    public string? GoalProcName { get; set; }

    /// <summary>Reduction-trace callback: (goalId, head, body).</summary>
    public Action<int, string, string>? OnReduction { get; set; }

    // Control trace output
    public bool ShowBindings { get; set; } = true;
    public bool DebugOutput { get; set; }

    /// <summary>Custom term formatter for consistent variable naming. (term, markReaders) → string.</summary>
    public Func<Term, bool, string>? TermFormatter { get; set; }

    /// <summary>Module context for distribute/transmit handlers (Phase 5 integration).</summary>
    public object? ModuleContext { get; set; }

    /// <summary>Stub-signature constructor preserved VERBATIM: (rt, goalId, pc).</summary>
    public RunnerContext(GlpRuntimeEngine rt, int goalId, int pc)
    {
        Rt = rt;
        GoalId = goalId;
        Pc = pc;
        Kappa = pc;
        Env = new CallEnv();
    }

    /// <summary>
    /// Rich constructor mirroring the Dart named-arg form. Additive overload — does not
    /// change the stub signature; callers that need the full set use this one.
    /// </summary>
    public RunnerContext(
        GlpRuntimeEngine rt,
        int goalId,
        int kappa,
        CallEnv? env = null,
        Action<GoalRef>? onActivation = null,
        int? reductionBudget = null,
        string? goalHead = null,
        string? goalProcName = null,
        Action<int, string, string>? onReduction = null,
        bool showBindings = true,
        bool debugOutput = false,
        Func<Term, bool, string>? termFormatter = null,
        object? moduleContext = null)
    {
        Rt = rt;
        GoalId = goalId;
        Kappa = kappa;
        Pc = kappa;
        Env = env ?? new CallEnv();
        OnActivation = onActivation;
        ReductionBudget = reductionBudget;
        GoalHead = goalHead;
        GoalProcName = goalProcName;
        OnReduction = onReduction;
        ShowBindings = showBindings;
        DebugOutput = debugOutput;
        TermFormatter = termFormatter;
        ModuleContext = moduleContext;
    }

    /// <summary>Attach (or replace) the call environment. Used by RunStep wiring.</summary>
    public void AttachEnv(CallEnv env) => Env = env;

    /// <summary>
    /// Re-format the goal head from current env state (after σ̂ applied to heap).
    /// Shows bound values instead of unbound variable names.
    /// </summary>
    public string ReformatHead()
    {
        var name = GoalProcName ?? GoalHead ?? "?";
        var args = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var arg = Env.Arg(i);
            if (arg != null)
            {
                args.Add(TermFormatter != null ? TermFormatter(arg, true) : arg.ToString() ?? "<null>");
            }
            else
            {
                break;
            }
        }
        if (args.Count == 0) return name;
        return $"{name}({string.Join(", ", args)})";
    }

    /// <summary>Reset clause-local state (σ̂w, Si, traversal state, clause vars).</summary>
    public void ClearClause()
    {
        SigmaHat.Clear();
        Si.Clear();
        InBody = false;
        Mode = UnifyMode.Read;
        S = 0;
        CurrentStructure = null;
        ClauseVars.Clear();
        GuardArgSlot = null;
        ParentStack.Clear();
    }
}

// ── ReplModuleTarget / ReplModuleContext ───────────────────────────────────

/// <summary>Module import target held by the REPL module context.</summary>
public sealed class ReplModuleTarget
{
    public string Name { get; }
    public BytecodeProgram Program { get; }

    public ReplModuleTarget(string name, BytecodeProgram program)
    {
        Name = name;
        Program = program;
    }
}

/// <summary>Simple module context used by the REPL for synchronous goal spawning.</summary>
public sealed class ReplModuleContext
{
    public string ModuleName { get; }

    /// <summary>importIndex (1-based) → target.</summary>
    public IReadOnlyDictionary<int, ReplModuleTarget> Imports { get; }

    /// <summary>Combined program for entry-point lookup.</summary>
    public BytecodeProgram? CombinedProgram { get; }

    /// <summary>Key for the scheduler's runners map.</summary>
    public string ProgramKey { get; }

    public ReplModuleContext(
        string moduleName,
        Dictionary<int, ReplModuleTarget> imports,
        BytecodeProgram? combinedProgram,
        string programKey)
    {
        ModuleName = moduleName;
        Imports = imports ?? new Dictionary<int, ReplModuleTarget>();
        CombinedProgram = combinedProgram;
        ProgramKey = programKey;
    }
}

// ── BytecodeRunner ─────────────────────────────────────────────────────────

/// <summary>
/// WAM/FCP bytecode interpreter.
///
/// The Dart entry is run(cx)/runWithStatus(cx); the C# public entry is RunStep(cx, env,
/// reductions). RunStep wires the supplied env + reduction budget into the context, then
/// runs the dispatch loop (the body of the Dart runWithStatus, minus the per-arm logic
/// which now lives in Exec* methods).
/// </summary>
public sealed class BytecodeRunner
{
    private readonly BytecodeProgram _prog;

    public BytecodeProgram Program => _prog;

    public BytecodeRunner(BytecodeProgram program)
    {
        _prog = program;
    }

    // ── Dispatch control signal ─────────────────────────────────────────────
    //
    // Each Exec* arm returns a _Step telling the loop what to do next. This mirrors the
    // Dart arm bodies, which either fall through (advance pc), assign pc (jump/continue),
    // or `return RunResult.X`. The skeleton loop interprets the signal; fill chunks return
    // the right signal from each arm.
    private enum _Action { Advance, Jump, Stop }

    private readonly struct _Step
    {
        public readonly _Action Kind;
        public readonly int NextPc;          // valid when Kind == Jump
        public readonly RunResult Result;    // valid when Kind == Stop

        private _Step(_Action kind, int nextPc, RunResult result)
        {
            Kind = kind;
            NextPc = nextPc;
            Result = result;
        }

        /// <summary>Advance to pc+1.</summary>
        public static _Step Advance() => new(_Action.Advance, 0, default);

        /// <summary>Set pc to an absolute address and continue the loop.</summary>
        public static _Step Jump(int pc) => new(_Action.Jump, pc, default);

        /// <summary>Terminate the loop, returning the given RunResult.</summary>
        public static _Step Stop(RunResult result) => new(_Action.Stop, 0, result);
    }

    // ── Public entry ─────────────────────────────────────────────────────────

    /// <summary>
    /// Execute one scheduling quantum for the given goal.
    /// Wires env + reduction budget into the context and runs the dispatch loop.
    /// </summary>
    public RunResult RunStep(RunnerContext cx, CallEnv env, int reductions)
    {
        cx.AttachEnv(env);
        cx.Kappa = cx.Pc;
        cx.ReductionBudget = reductions < 0 ? (int?)null : reductions;
        cx.ReductionsUsed = 0;
        return RunWithStatus(cx);
    }

    /// <summary>Dart run(cx): run to completion, discarding the status.</summary>
    public void Run(RunnerContext cx) => RunWithStatus(cx);

    /// <summary>
    /// The dispatch loop. Mirrors the Dart runWithStatus while/if-cascade: each opcode is
    /// dispatched to its Exec* arm, and the returned _Step drives pc/termination.
    /// </summary>
    public RunResult RunWithStatus(RunnerContext cx)
    {
        var pc = cx.Kappa; // Start at goal's entry point (not 0!)
        var ops = _prog.Instructions;

        while (pc < ops.Count)
        {
            // Check reduction budget.
            if (cx.ReductionBudget != null && cx.ReductionsUsed >= cx.ReductionBudget.Value)
            {
                cx.Pc = pc;
                return RunResult.OutOfReductions;
            }
            cx.ReductionsUsed++;

            cx.Pc = pc;
            var op = ops[pc];
            EquivTrace.Op(op.GetType().Name, pc);  // feature-020 spine (no-op unless GLP_EQUIV_TRACE)
            var step = Dispatch(cx, op);

            switch (step.Kind)
            {
                case _Action.Advance:
                    pc++;
                    continue;
                case _Action.Jump:
                    pc = step.NextPc;
                    continue;
                case _Action.Stop:
                    cx.Pc = pc;
                    return step.Result;
            }
        }

        return RunResult.Terminated;
    }

    // ── Opcode dispatch ────────────────────────────────────────────────────
    //
    // Type-switch over the opcode object. v1 (IOp) and v2 (IOpV2) opcodes are dispatched
    // to one Exec* arm each. The per-arm logic is filled by later chunks.
    private _Step Dispatch(RunnerContext cx, object op) => op switch
    {
        // ── v1 opcodes ──
        Allocate o => ExecAllocate(cx, o),
        BodySetConst o => ExecBodySetConst(cx, o),
        BodySetConstArg o => ExecBodySetConstArg(cx, o),
        BodySetStructConstArgs o => ExecBodySetStructConstArgs(cx, o),
        ClauseNext o => ExecClauseNext(cx, o),
        ClauseTry o => ExecClauseTry(cx, o),
        Commit o => ExecCommit(cx, o),
        Deallocate o => ExecDeallocate(cx, o),
        Distribute o => ExecDistribute(cx, o),
        GetValue o => ExecGetValue(cx, o),
        GetVariable o => ExecGetVariable(cx, o),
        Ground o => ExecGround(cx, o),
        GroundEqual o => ExecGroundEqual(cx, o),
        Guard o => ExecGuard(cx, o),
        GuardFail o => ExecGuardFail(cx, o),
        GuardNeedReader o => ExecGuardNeedReader(cx, o),
        GuardNeedReaderArg o => ExecGuardNeedReaderArg(cx, o),
        Halt o => ExecHalt(cx, o),
        HeadBindWriter o => ExecHeadBindWriter(cx, o),
        HeadBindWriterArg o => ExecHeadBindWriterArg(cx, o),
        HeadConstant o => ExecHeadConstant(cx, o),
        HeadList o => ExecHeadList(cx, o),
        HeadNil o => ExecHeadNil(cx, o),
        HeadStructure o => ExecHeadStructure(cx, o),
        Known o => ExecKnown(cx, o),
        Label o => ExecLabel(cx, o),
        NoMoreClauses o => ExecNoMoreClauses(cx, o),
        NoReaders o => ExecNoReaders(cx, o),
        Nop o => ExecNop(cx, o),
        Otherwise o => ExecOtherwise(cx, o),
        Pop o => ExecPop(cx, o),
        Proceed o => ExecProceed(cx, o),
        Push o => ExecPush(cx, o),
        PutBoundConst o => ExecPutBoundConst(cx, o),
        PutBoundNil o => ExecPutBoundNil(cx, o),
        PutConstant o => ExecPutConstant(cx, o),
        PutList o => ExecPutList(cx, o),
        PutNil o => ExecPutNil(cx, o),
        PutStructure o => ExecPutStructure(cx, o),
        Requeue o => ExecRequeue(cx, o),
        RequireReaderArg o => ExecRequireReaderArg(cx, o),
        RequireWriterArg o => ExecRequireWriterArg(cx, o),
        ResetAndGoto o => ExecResetAndGoto(cx, o),
        SetConstant o => ExecSetConstant(cx, o),
        Spawn o => ExecSpawn(cx, o),
        SuspendEnd o => ExecSuspendEnd(cx, o),
        TailStep o => ExecTailStep(cx, o),
        Transmit o => ExecTransmit(cx, o),
        TryNextClause o => ExecTryNextClause(cx, o),
        UnifyConstant o => ExecUnifyConstant(cx, o),
        UnifyStructure o => ExecUnifyStructure(cx, o),
        UnifyVoid o => ExecUnifyVoid(cx, o),
        UnionSiAndGoto o => ExecUnionSiAndGoto(cx, o),

        // ── v2 opcodes ──
        V2.GetValue o => ExecV2GetValue(cx, o),
        V2.GetVariable o => ExecV2GetVariable(cx, o),
        V2.HeadVariable o => ExecV2HeadVariable(cx, o),
        V2.PutVariable o => ExecV2PutVariable(cx, o),
        V2.SetVariable o => ExecV2SetVariable(cx, o),
        V2.UnifyVariable o => ExecV2UnifyVariable(cx, o),
        V2.Unknown o => ExecV2Unknown(cx, o),

        _ => _Step.Advance(), // default progress (Dart: pc++)
    };

    // ── Loop-internal helpers (faithful translations — used by the arms) ─────

    /// <summary>
    /// Numeric-aware equality for constant VALUES, converging HEAD/UNIFY constant
    /// matching to Dart's <c>==</c> on <c>num</c> (cross-type: <c>0 == 0.0</c>).
    /// The prior type-strict <c>object.Equals</c> made an arithmetic-produced
    /// <c>double 0.0</c> fail to match an integer literal <c>0</c>, so recursive
    /// base clauses (e.g. <c>producer([], 0)</c>) never committed and the writer
    /// tail was left open → spurious <c>→ failed</c>. For non-numeric values this
    /// falls back to <c>object.Equals</c>, so atoms/strings/<c>nil</c> compare
    /// exactly as before (safe drop-in). Mirrors runner.dart's <c>value != op.value</c>.
    /// </summary>
    private static bool NumEquals(object? a, object? b)
    {
        if (a is null || b is null) return Equals(a, b);
        bool aNum = a is long or int or double;
        bool bNum = b is long or int or double;
        if (aNum && bNum)
        {
            // One side double → promote both to double (Dart int==double semantics);
            // both integral → exact 64-bit compare (no precision loss).
            if (a is double || b is double)
                return Convert.ToDouble(a) == Convert.ToDouble(b);
            return Convert.ToInt64(a) == Convert.ToInt64(b);
        }
        return Equals(a, b);
    }

    /// <summary>
    /// Find next ClauseTry instruction after current PC. If no more ClauseTry, look for
    /// ClauseNext / SuspendEnd / NoMoreClauses to check for suspension/failure.
    /// </summary>
    private int _findNextClauseTry(int fromPc)
    {
        var ops = _prog.Instructions;
        for (var i = fromPc + 1; i < ops.Count; i++)
        {
            if (ops[i] is ClauseNext) return i; // ClauseNext first (unions Si to U)
            if (ops[i] is ClauseTry) return i;
            if (ops[i] is SuspendEnd) return i; // Jump to SUSP to check U
            if (ops[i] is NoMoreClauses) return i; // Jump to NoMoreClauses to check U
        }
        return ops.Count; // End of program if no more clauses or SUSP
    }

    /// <summary>
    /// Soft-fail to next clause: merge Si into U, clear clause state, jump to next ClauseTry.
    /// U is NOT cleared — it accumulates across clause attempts.
    /// </summary>
    private void _softFailToNextClause(RunnerContext cx, int currentPc)
    {
        cx.U.UnionWith(cx.Si);
        cx.ClearClause();
    }

    /// <summary>
    /// Find the final unbound variable in a chain (FCP: follow var→var bindings).
    /// derefAddr already follows the FULL chain, so we use it once.
    /// </summary>
    private int _finalUnboundVar(RunnerContext cx, int addr)
    {
        var derefResult = cx.Rt.Heap.DerefAddr(addr);

        if (derefResult is VarRef vr)
        {
            var finalAddr = vr.Addr;
            var isWriter = cx.Rt.Heap.IsWriter(finalAddr);
            // Goals suspend on READERS, not writers. If the final unbound var is a writer,
            // return its paired reader (spec v3.2: pairedReaderAddr, not +1 arithmetic).
            var readerAddr = isWriter ? cx.Rt.Heap.PairedReaderAddr(finalAddr) : finalAddr;
            return readerAddr;
        }

        // Writer is bound to a ground term, reader is effectively bound.
        return addr;
    }

    /// <summary>
    /// Suspend on unbound reader: add to U and fail to next clause atomically.
    /// Returns the PC of the next clause try.
    /// </summary>
    private int _suspendAndFail(RunnerContext cx, int readerId, int currentPc)
    {
        cx.U.Add(readerId);
        _softFailToNextClause(cx, currentPc);
        return _findNextClauseTry(currentPc);
    }

    /// <summary>Suspend on multiple unbound readers: add all to U and fail to next clause.</summary>
    private int _suspendAndFailMulti(RunnerContext cx, ISet<int> readerIds, int currentPc)
    {
        cx.U.UnionWith(readerIds);
        _softFailToNextClause(cx, currentPc);
        return _findNextClauseTry(currentPc);
    }

    /// <summary>Helper to get argument term from the call environment.</summary>
    private Term? _getArg(RunnerContext cx, int slot) => cx.Env.Arg(slot);

    /// <summary>Format a term for display (faithful translation of the Dart static helper).</summary>
    internal static string _formatTerm(GlpRuntimeEngine rt, Term term, bool markReaders = true)
    {
        if (term is ConstTerm constTerm)
        {
            if (Equals(constTerm.Value, "nil")) return "[]";
            if (constTerm.Value == null) return "<null>";
            return constTerm.Value.ToString() ?? "<null>";
        }
        if (term is VarRef writerRef && rt.Heap.IsWriter(writerRef.Addr))
        {
            var wid = writerRef.Addr;
            if (rt.Heap.IsWriterBound(wid))
            {
                var value = rt.Heap.ValueOfWriter(wid);
                if (value != null) return _formatTerm(rt, value, markReaders);
            }
            var displayId = wid >= 1000 ? wid - 1000 : wid;
            return $"X{displayId}";
        }
        if (term is VarRef readerRef && rt.Heap.IsReader(readerRef.Addr))
        {
            var rid = readerRef.Addr;
            if (rt.Heap.IsReaderBound(rid))
            {
                var value = rt.Heap.GetReaderValue(rid);
                if (value != null) return _formatTerm(rt, value, markReaders);
            }
            var displayId = rid >= 1000 ? rid - 1000 : rid;
            return markReaders ? $"X{displayId}?" : $"X{displayId}";
        }
        if (term is StructTerm structTerm)
        {
            // Special formatting for list structures.
            if (structTerm.Functor == "." && structTerm.Args.Count == 2)
            {
                var elements = new List<string>();
                Term listTerm = structTerm;
                var visited = new HashSet<int>();

                while (true)
                {
                    if (listTerm is not StructTerm st || st.Functor != ".") break;

                    var head = st.Args[0];
                    var tail = st.Args[1];

                    var headStr = _formatTerm(rt, head, markReaders);

                    if (head is VarRef hv && visited.Contains(hv.Addr))
                    {
                        headStr = "<circular>";
                    }
                    else if (head is VarRef hv2)
                    {
                        visited.Add(hv2.Addr);
                    }

                    elements.Add(headStr);

                    if (tail is ConstTerm tc && (Equals(tc.Value, "nil") || tc.Value == null))
                    {
                        break; // Proper list ending
                    }
                    if (tail is StructTerm tst && tst.Functor == ".")
                    {
                        listTerm = tst;
                        continue;
                    }
                    if (tail is VarRef tv)
                    {
                        if (visited.Contains(tv.Addr))
                            return $"[{string.Join(", ", elements)} | <circular>]";
                        visited.Add(tv.Addr);
                        var tailStr = _formatTerm(rt, tv, markReaders);
                        return $"[{string.Join(", ", elements)} | {tailStr}]";
                    }
                    // Non-list tail
                    var ts = _formatTerm(rt, tail, markReaders);
                    return $"[{string.Join(", ", elements)} | {ts}]";
                }

                return $"[{string.Join(", ", elements)}]";
            }

            // General structure formatting.
            var args = new List<string>();
            foreach (var a in structTerm.Args) args.Add(_formatTerm(rt, a, markReaders));
            return $"{structTerm.Functor}({string.Join(",", args)})";
        }
        return term.ToString() ?? "<null>";
    }

    // ── Opcode arms (STUBBED — filled by later chunks) ───────────────────────
    // Each preserves the exact opcode type it handles; bodies move in verbatim later.

    private _Step ExecAllocate(RunnerContext cx, Allocate op)
    {
        var pc = cx.Pc;
        // allocate N: Create environment frame with N permanent variable slots
        // WAM semantics: E' = newFrame(E, CP, N); CP = P+1
        // Used by non-tail-recursive predicates to save local state
        if (!cx.InBody)
        {
            throw new InvalidOperationException("Allocate must be in BODY phase (after commit)");
        }

        var newFrame = new EnvironmentFrame(
            parent: cx.E,
            continuationPointer: cx.CP ?? (pc + 1), // Save continuation (next instruction)
            size: (int)op.Slots);

        cx.E = newFrame;
        cx.CP = pc + 1; // Update CP to point to next instruction

        return _Step.Advance();
    }

    private _Step ExecBodySetConst(RunnerContext cx, BodySetConst op)
    {
        if (cx.InBody)
        {
            // bindWriterConst now returns activations (FCP: all bindings wake goals)
            var acts = cx.Rt.Heap.BindWriterConst((int)op.WriterId, op.Value);
            foreach (var a in acts)
            {
                cx.Rt.Gq.Enqueue(a);
                cx.OnActivation?.Invoke(a);
            }
        }
        return _Step.Advance();
    }

    private _Step ExecBodySetConstArg(RunnerContext cx, BodySetConstArg op)
    {
        var arg = cx.Env.Arg((int)op.Slot);
        int? writerAddr = (arg is VarRef avr && cx.Rt.Heap.IsWriter(avr.Addr)) ? avr.Addr : (int?)null;
        if (cx.InBody && writerAddr != null)
        {
            // bindWriterConst now returns activations (FCP: all bindings wake goals)
            var acts = cx.Rt.Heap.BindWriterConst(writerAddr.Value, op.Value);
            foreach (var a in acts)
            {
                cx.Rt.Gq.Enqueue(a);
                cx.OnActivation?.Invoke(a);
            }
        }
        return _Step.Advance();
    }

    private _Step ExecBodySetStructConstArgs(RunnerContext cx, BodySetStructConstArgs op)
    {
        if (cx.InBody)
        {
            var args = new List<Term>(op.ConstArgs.Count);
            foreach (var v in op.ConstArgs)
            {
                args.Add(v is Term vt ? vt : new ConstTerm(v));
            }
            // bindWriterStruct now returns activations (FCP: all bindings wake goals)
            var acts = cx.Rt.Heap.BindWriterStruct((int)op.WriterId, op.Functor, args);
            foreach (var a in acts)
            {
                cx.Rt.Gq.Enqueue(a);
                cx.OnActivation?.Invoke(a);
            }
        }
        return _Step.Advance();
    }

    private _Step ExecClauseNext(RunnerContext cx, ClauseNext op)
    {
        // clause_next: Unified instruction for moving to next clause (spec 2.2)
        // Discard σ̂w, union Si into U, clear clause state, jump to next clause
        cx.U.UnionWith(cx.Si);
        cx.ClearClause();
        return _Step.Jump(_prog.Labels[op.Label]);
    }

    private _Step ExecClauseTry(RunnerContext cx, ClauseTry op)
    {
        cx.ClearClause();
        return _Step.Advance();
    }

    private _Step ExecCommit(RunnerContext cx, Commit op)
    {
        var pc = cx.Pc;
        // Phase 2: Resolve Si against σ̂w (two-phase HEAD unification)
        var resolvedSi = new HashSet<int>();
        foreach (var readerAddr in cx.Si)
        {
            // Use tryWriterForReader to handle imported readers gracefully
            var writerAddr = cx.Rt.Heap.TryWriterForReader(readerAddr);
            // Imported reader (null) or writer not in σ̂w -> unresolved
            if (writerAddr == null || !cx.SigmaHat.ContainsKey(writerAddr.Value))
            {
                resolvedSi.Add(readerAddr);
            }
        }

        if (resolvedSi.Count > 0)
        {
            cx.U.UnionWith(resolvedSi);
            cx.Si.Clear();
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }
        cx.Si.Clear();

        // Commit only reached if HEAD and GUARD phases succeeded.
        // Apply σ̂w to heap atomically.

        // Convert tentative structures to real Terms before committing
        var convertedSigmaHat = new Dictionary<int, object?>();
        foreach (var entry in cx.SigmaHat)
        {
            var writerAddr = entry.Key;
            var value = entry.Value;

            if (value is _TentativeStruct tstruct)
            {
                // Convert tentative structure to StructTerm
                var termArgs = new List<Term>();
                foreach (var arg in tstruct.Args)
                {
                    if (arg is _ClauseVar clauseVar)
                    {
                        // Clause variable placeholder - need to resolve to actual writer/reader.
                        // Check if already resolved in clauseVars.
                        cx.ClauseVars.TryGetValue(clauseVar.VarIndex, out var resolved);
                        if (resolved is VarRef resolvedRef)
                        {
                            // Already a VarRef - use it directly or extract reader if needed.
                            var isResolvedWriter = cx.Rt.Heap.IsWriter(resolvedRef.Addr);
                            if (clauseVar.IsWriter && isResolvedWriter)
                            {
                                // Writer placeholder, resolved to writer VarRef - use as-is.
                                termArgs.Add(resolvedRef);
                            }
                            else if (clauseVar.IsWriter && !isResolvedWriter)
                            {
                                // Writer placeholder but resolved to reader? Get paired writer.
                                // Use tryWriterForReader for imported reader support.
                                var wid = cx.Rt.Heap.TryWriterForReader(resolvedRef.Addr);
                                if (wid != null)
                                {
                                    termArgs.Add(new VarRef(wid.Value));
                                }
                                else
                                {
                                    // Imported reader - no local writer, use reader as-is.
                                    termArgs.Add(resolvedRef);
                                }
                            }
                            else if (!clauseVar.IsWriter && !isResolvedWriter)
                            {
                                // Reader placeholder, resolved to reader VarRef - use as-is.
                                termArgs.Add(resolvedRef);
                            }
                            else // (!clauseVar.IsWriter && isResolvedWriter)
                            {
                                // Reader placeholder but resolved to writer? Use reader addr (writer + 1).
                                termArgs.Add(new VarRef(resolvedRef.Addr + 1));
                            }
                        }
                        else if (resolved is Term resolvedTerm)
                        {
                            // Already a term - use as-is.
                            termArgs.Add(resolvedTerm);
                        }
                        else
                        {
                            // Not yet resolved - create fresh variable.
                            var (freshWriterAddr, freshReaderAddr) = cx.Rt.Heap.AllocateVariable();
                            // Store appropriate VarRef in clauseVars.
                            cx.ClauseVars[clauseVar.VarIndex] = new VarRef(clauseVar.IsWriter ? freshWriterAddr : freshReaderAddr);
                            if (clauseVar.IsWriter)
                            {
                                termArgs.Add(new VarRef(freshWriterAddr));
                            }
                            else
                            {
                                termArgs.Add(new VarRef(freshReaderAddr));
                            }
                        }
                    }
                    else if (arg is _TentativeStruct nestedTentative)
                    {
                        // Nested tentative structure - recursively convert.
                        termArgs.Add(_convertTentativeToStruct(nestedTentative, cx));
                    }
                    else if (arg == null)
                    {
                        // Void/unbound - leave as null constant.
                        termArgs.Add(new ConstTerm(null));
                    }
                    else if (arg is Term argTerm)
                    {
                        // Already a Term (ConstTerm, StructTerm, etc.) - use as-is.
                        termArgs.Add(argTerm);
                    }
                    else
                    {
                        // Raw constant value - wrap in ConstTerm.
                        termArgs.Add(new ConstTerm(arg));
                    }
                }
                convertedSigmaHat[writerAddr] = new StructTerm(tstruct.Functor, termArgs);
            }
            else
            {
                // Direct value (constant)
                convertedSigmaHat[writerAddr] = value;
            }
        }

        // Enforce WxW: writer→writer bindings are prohibited.
        foreach (var entry in convertedSigmaHat)
        {
            var writerAddr = entry.Key;
            var value = entry.Value;
            if (value is VarRef valueRef && cx.Rt.Heap.IsWriter(valueRef.Addr))
            {
                throw new InvalidOperationException(
                    $"WxW violation in commit: W{writerAddr} → W{valueRef.Addr} (both unbound writers)");
            }
        }

        // Apply σ̂w: bind writers to tentative values, then wake suspended goals.
        var acts = CommitOps.ApplySigmaHatFCP(cx.Rt.Heap, convertedSigmaHat);

        // feature-020 equiv-trace: a PROCEEDING Commit (past the resolvedSi early
        // soft-fail) = successful two-phase HEAD unification; each σ̂w entry is a
        // WRITER_BIND. The Commit op itself is emitted HERE (not from the dispatch
        // loop) to match Dart's conditional COMMIT print (runner.dart:2400) — Dart
        // stays silent on the resolvedSi early-exit path. No-op unless enabled.
        if (EquivTrace.Enabled)
        {
            EquivTrace.OpAt("Commit", pc);
            EquivTrace.Unify("success", convertedSigmaHat.Keys);
            foreach (var b in convertedSigmaHat)
                EquivTrace.WriterBind(b.Key, b.Value);
        }

        foreach (var a in acts)
        {
            EquivTrace.Reactivate(a.Id);  // feature-020 (no-op unless enabled)
            cx.Rt.Gq.Enqueue(a);
            cx.OnActivation?.Invoke(a);
        }

        cx.SigmaHat.Clear();
        // Clear argument registers after commit (guards may have set them up).
        cx.ArgSlots.Clear();
        // Reset structure building state for BODY phase.
        cx.CurrentStructure = null;
        cx.S = 0;
        cx.Mode = UnifyMode.Read;
        cx.ParentStack.Clear();
        cx.InBody = true;
        return _Step.Advance();
    }

    private _Step ExecDeallocate(RunnerContext cx, Deallocate op)
    {
        // deallocate: Remove current environment frame
        // WAM semantics: CP = E.CP; E = E.parent; P = CP
        // Restores previous environment and returns to saved continuation
        if (cx.E == null)
        {
            throw new InvalidOperationException("Deallocate with no environment frame");
        }

        var frame = cx.E!;
        cx.CP = frame.ContinuationPointer; // Restore continuation pointer
        cx.E = frame.Parent;               // Restore previous environment

        // Note: Unlike WAM, we don't jump to CP here - deallocate just pops the frame.
        // The subsequent proceed or return instruction will handle the jump.
        return _Step.Advance();
    }

    private _Step ExecDistribute(RunnerContext cx, Distribute op)
    {
        // Static RPC to imported module at known index
        // Following FCP: distribute # {Index, Goal}
        // Routes RPC via GLP channels or REPL module context.
        if (cx.InBody)
        {
            // Collect arguments from argSlots
            var args = new List<Term>();
            for (var i = 0; i < (int)op.Arity; i++)
            {
                cx.ArgSlots.TryGetValue(i, out var arg);
                if (arg != null) args.Add(arg);
            }

            // Check if module context is available
            if (cx.ModuleContext is ReplModuleContext replCtx)
            {
                // REPL mode: directly spawn goal in target module
                ReplModuleTarget? target = replCtx.Imports.TryGetValue((int)op.ImportIndex, out var t) ? t : null;

                if (target != null)
                {
                    // Check GLP channel first (Phase 5: RPC routing via GLP channels)
                    if (cx.Rt.GlpChannels.TryGetValue(target.Name, out var glpChannel) && glpChannel != null)
                    {
                        // Route via GLP channel — build goal term, send on channel
                        var goalTerm = new StructTerm(op.Functor, args);
                        var activations = glpChannel.Send(goalTerm);
                        foreach (var act in activations)
                        {
                            cx.Rt.EnqueueReactivatedGoal(act);
                        }
                        if (cx.DebugOutput)
                        {
                            Console.WriteLine($"[MODULE] Distribute (GLP channel): {replCtx.ModuleName} -> {target.Name} # {op.Functor}/{op.Arity}");
                        }
                    }
                    else
                    {
                        // Module not activated — no GLP channel available
                        Console.WriteLine($"ERROR: Distribute: module {target.Name} not activated (no GLP channel for {op.Functor}/{op.Arity})");
                        return _Step.Stop(RunResult.Terminated);
                    }
                }
                else
                {
                    Console.WriteLine($"ERROR: Distribute: no target for import index {op.ImportIndex} ({op.Functor}/{op.Arity})");
                    return _Step.Stop(RunResult.Terminated);
                }
            }
            else
            {
                // No module context
                Console.WriteLine($"ERROR: Distribute: no module context for import[{op.ImportIndex}] # {op.Functor}/{op.Arity}");
                return _Step.Stop(RunResult.Terminated);
            }
            cx.ArgSlots.Clear();
        }
        return _Step.Advance();
    }

    private _Step ExecGetValue(RunnerContext cx, GetValue op)
    {
        var pc = cx.Pc;
        // Unify argument with clause variable (subsequent occurrence)
        var arg = _getArg(cx, (int)op.ArgSlot);
        if (arg == null)
        {
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        // Get the previously stored value
        cx.ClauseVars.TryGetValue((int)op.VarIndex, out var storedValue);
        if (storedValue == null)
        {
            // Variable not initialized - error
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        // Unify argument with stored value
        if (arg is VarRef wv && cx.Rt.Heap.IsWriter(wv.Addr))
        {
            // Argument is writer VarRef - bind it to stored value in σ̂w
            if (storedValue is VarRef svw && cx.Rt.Heap.IsWriter(svw.Addr))
            {
                // storedValue is a writer VarRef - check they match
                if (wv.Addr != svw.Addr)
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else if (storedValue is int sviw)
            {
                // Legacy: bare writer addr - check they match
                if (wv.Addr != sviw)
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else if (storedValue is VarRef svr && cx.Rt.Heap.IsReader(svr.Addr))
            {
                // storedValue is a reader (e.g., Xs?) - bind writer to reader's value
                var readerAddr = svr.Addr;
                if (cx.Rt.Heap.IsReaderBound(readerAddr))
                {
                    // Reader is bound - bind arg writer to that value
                    var readerValue = cx.Rt.Heap.GetReaderValue(readerAddr);
                    cx.SigmaHat[wv.Addr] = readerValue;
                }
                else
                {
                    // Reader is unbound - add reader to Si (suspend)
                    return _Step.Jump(_suspendAndFail(cx, readerAddr, pc));
                }
            }
            else
            {
                // storedValue is a Term - bind writer to it
                cx.SigmaHat[wv.Addr] = storedValue;
            }
        }
        else if (arg is VarRef rv && cx.Rt.Heap.IsReader(rv.Addr))
        {
            // Argument is reader VarRef - verify it matches stored value
            if (storedValue is VarRef svr2 && cx.Rt.Heap.IsReader(svr2.Addr))
            {
                // storedValue is also a reader - fail definitively
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }

            var bound = cx.Rt.Heap.IsReaderBound(rv.Addr);
            if (bound)
            {
                // Reader is bound - check value matches
                var readerValue = cx.Rt.Heap.GetReaderValue(rv.Addr);
                if (storedValue is Term svt)
                {
                    if (!Equals(readerValue, svt))
                    {
                        _softFailToNextClause(cx, pc);
                        return _Step.Jump(_findNextClauseTry(pc));
                    }
                }
                else if (storedValue is int sviw2)
                {
                    // storedValue is a writer addr - check if they point to same writer
                    var wid = cx.Rt.Heap.TryWriterForReader(rv.Addr);
                    if (wid == null || wid.Value != sviw2)
                    {
                        _softFailToNextClause(cx, pc);
                        return _Step.Jump(_findNextClauseTry(pc));
                    }
                }
            }
            else if (storedValue is int sviw3)
            {
                // Reader unbound, storedValue is writer addr - check if they match
                var wid = cx.Rt.Heap.TryWriterForReader(rv.Addr);
                if (wid == null || wid.Value != sviw3)
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else
            {
                // Reader unbound, storedValue is a Term - add to Si
                var suspendOnVar = _finalUnboundVar(cx, rv.Addr);
                return _Step.Jump(_suspendAndFail(cx, suspendOnVar, pc));
            }
        }
        else
        {
            // Ground term - TODO: handle ConstTerm/StructTerm
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }
        return _Step.Advance();
    }

    private _Step ExecGetVariable(RunnerContext cx, GetVariable op)
    {
        var pc = cx.Pc;
        // Load argument into clause variable (first occurrence)
        var arg = _getArg(cx, (int)op.ArgSlot);
        if (arg == null)
        {
            // No argument provided
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        // Store argument value in clauseVars
        if (arg is VarRef wv && cx.Rt.Heap.IsWriter(wv.Addr))
        {
            cx.ClauseVars[(int)op.VarIndex] = wv.Addr;
        }
        else if (arg is VarRef rv && cx.Rt.Heap.IsReader(rv.Addr))
        {
            // Reader VarRef - store directly WITHOUT suspending.
            cx.ClauseVars[(int)op.VarIndex] = arg;
        }
        else if (arg is ConstTerm || arg is StructTerm)
        {
            // Ground term - store directly
            cx.ClauseVars[(int)op.VarIndex] = arg;
        }
        else
        {
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }
        return _Step.Advance();
    }

    private _Step ExecGround(RunnerContext cx, Ground op)
    {
        var pc = cx.Pc;
        // ground(X): Succeeds if X is ground (contains no unbound variables)
        // ~ground(X): Succeeds if X is NOT ground (contains unbound variables)
        cx.ClauseVars.TryGetValue((int)op.VarIndex, out var value);
        if (value == null)
        {
            // Variable doesn't exist - fail (even for negated)
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        // Collect unbound readers and check for unbound writers
        // NOTE: Must check BOTH sigmaHat (tentative bindings) AND heap bindings
        // CYCLE DETECTION: Track visited variable addresses to handle circular terms
        var unboundReaders = new HashSet<int>();
        var visited = new HashSet<int>(); // Track visited variable addresses for cycle detection
        var hasUnboundWriter = false;

        void CollectUnbound(object? term)
        {
            if (term is VarRef wterm && cx.Rt.Heap.IsWriter(wterm.Addr))
            {
                var writerAddr = wterm.Addr;
                // Cycle detection: skip already-visited variables
                if (visited.Contains(writerAddr)) return;
                visited.Add(writerAddr);
                // First check sigmaHat for tentative binding
                if (cx.SigmaHat.TryGetValue(writerAddr, out var sigmaBinding) && sigmaBinding != null)
                {
                    CollectUnbound(sigmaBinding);
                }
                else if (!cx.Rt.Heap.IsFullyBound(writerAddr))
                {
                    hasUnboundWriter = true;
                }
                else
                {
                    CollectUnbound(cx.Rt.Heap.GetValue(writerAddr));
                }
            }
            else if (term is VarRef rterm && cx.Rt.Heap.IsReader(rterm.Addr))
            {
                var readerAddr = rterm.Addr;
                // Cycle detection: skip already-visited variables
                if (visited.Contains(readerAddr)) return;
                visited.Add(readerAddr);
                // First check sigmaHat for tentative binding on the reader
                if (cx.SigmaHat.TryGetValue(readerAddr, out var sigmaBinding) && sigmaBinding != null)
                {
                    CollectUnbound(sigmaBinding);
                }
                else
                {
                    // Use isReaderBound for imported reader support
                    if (!cx.Rt.Heap.IsReaderBound(readerAddr))
                    {
                        unboundReaders.Add(readerAddr);
                    }
                    else
                    {
                        CollectUnbound(cx.Rt.Heap.GetReaderValue(readerAddr));
                    }
                }
            }
            else if (term is StructTerm st)
            {
                foreach (var arg in st.Args) CollectUnbound(arg);
            }
            else if (term is _TentativeStruct ts)
            {
                // Tentative structure from HEAD phase - check its args
                foreach (var arg in ts.Args) CollectUnbound(arg);
            }
            // Constants contribute nothing
        }

        // Dereference the clause variable
        if (value is int valueInt)
        {
            // Could be writer addr or reader addr - check sigmaHat first
            if (cx.SigmaHat.TryGetValue(valueInt, out var sigmaBinding) && sigmaBinding != null)
            {
                CollectUnbound(sigmaBinding);
            }
            else if (cx.Rt.Heap.IsWriter(valueInt))
            {
                // It's a writer address
                if (!cx.Rt.Heap.IsFullyBound(valueInt))
                {
                    hasUnboundWriter = true;
                }
                else
                {
                    CollectUnbound(cx.Rt.Heap.GetValue(valueInt));
                }
            }
            else
            {
                // It's a reader address - use isReaderBound for imported reader support
                if (!cx.Rt.Heap.IsReaderBound(valueInt))
                {
                    unboundReaders.Add(valueInt);
                }
                else
                {
                    CollectUnbound(cx.Rt.Heap.GetReaderValue(valueInt));
                }
            }
        }
        else
        {
            // It's a Term - analyze it
            CollectUnbound(value);
        }

        // Decision logic (three-valued) with negation support:
        if (op.Negated)
        {
            // ~ground(X) semantics
            if (hasUnboundWriter)
            {
                // Contains unbound writer(s) → definitely not ground → SUCCEED
                return _Step.Advance();
            }
            else if (unboundReaders.Count > 0)
            {
                // Contains unbound readers → might become ground → SUSPEND
                return _Step.Jump(_suspendAndFailMulti(cx, unboundReaders, pc));
            }
            else
            {
                // No unbound variables → is ground → FAIL
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
        }
        else
        {
            // ground(X) semantics (original)
            if (hasUnboundWriter)
            {
                // Contains unbound writer(s) → FAIL (cannot become ground via SRSW)
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
            else if (unboundReaders.Count > 0)
            {
                // Contains unbound readers but no unbound writers → SUSPEND
                return _Step.Jump(_suspendAndFailMulti(cx, unboundReaders, pc));
            }
            else
            {
                // No unbound variables → SUCCEED (is ground)
                return _Step.Advance();
            }
        }
    }

    private _Step ExecGroundEqual(RunnerContext cx, GroundEqual op)
    {
        var pc = cx.Pc;
        // Ground equality test: X =?= Y
        // Succeeds if both arguments are ground and structurally equal.
        cx.ClauseVars.TryGetValue((int)op.LeftVarIndex, out var leftValue);
        cx.ClauseVars.TryGetValue((int)op.RightVarIndex, out var rightValue);

        if (leftValue == null || rightValue == null)
        {
            // Variable doesn't exist - fail
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        // Collect unbound readers and check for unbound writers in both terms
        var unboundReaders = new HashSet<int>();
        var visited = new HashSet<int>(); // Cycle detection
        var hasUnboundWriter = false;

        void CollectUnbound(object? term)
        {
            if (term is VarRef wterm && cx.Rt.Heap.IsWriter(wterm.Addr))
            {
                var writerAddr = wterm.Addr;
                if (visited.Contains(writerAddr)) return;
                visited.Add(writerAddr);
                // Check sigmaHat first for tentative binding
                if (cx.SigmaHat.TryGetValue(writerAddr, out var sigmaBinding) && sigmaBinding != null)
                {
                    CollectUnbound(sigmaBinding);
                }
                else if (!cx.Rt.Heap.IsFullyBound(writerAddr))
                {
                    hasUnboundWriter = true;
                }
                else
                {
                    CollectUnbound(cx.Rt.Heap.GetValue(writerAddr));
                }
            }
            else if (term is VarRef rterm && cx.Rt.Heap.IsReader(rterm.Addr))
            {
                var readerAddr = rterm.Addr;
                if (visited.Contains(readerAddr)) return;
                visited.Add(readerAddr);
                // Check sigmaHat first
                if (cx.SigmaHat.TryGetValue(readerAddr, out var sigmaBinding) && sigmaBinding != null)
                {
                    CollectUnbound(sigmaBinding);
                }
                else
                {
                    // Use isReaderBound for imported reader support
                    if (!cx.Rt.Heap.IsReaderBound(readerAddr))
                    {
                        unboundReaders.Add(readerAddr);
                    }
                    else
                    {
                        CollectUnbound(cx.Rt.Heap.GetReaderValue(readerAddr));
                    }
                }
            }
            else if (term is StructTerm st)
            {
                foreach (var arg in st.Args) CollectUnbound(arg);
            }
            else if (term is _TentativeStruct ts)
            {
                foreach (var arg in ts.Args) CollectUnbound(arg);
            }
            else if (term is int termInt)
            {
                // Bare int could be writer addr or reader addr
                if (visited.Contains(termInt)) return;
                visited.Add(termInt);
                if (cx.SigmaHat.TryGetValue(termInt, out var sigmaBinding) && sigmaBinding != null)
                {
                    CollectUnbound(sigmaBinding);
                }
                else if (cx.Rt.Heap.IsWriter(termInt))
                {
                    // It's a writer address
                    if (!cx.Rt.Heap.IsFullyBound(termInt))
                    {
                        hasUnboundWriter = true;
                    }
                    else
                    {
                        CollectUnbound(cx.Rt.Heap.GetValue(termInt));
                    }
                }
                else
                {
                    // It's a reader address - use isReaderBound for imported reader support
                    if (!cx.Rt.Heap.IsReaderBound(termInt))
                    {
                        unboundReaders.Add(termInt);
                    }
                    else
                    {
                        CollectUnbound(cx.Rt.Heap.GetReaderValue(termInt));
                    }
                }
            }
            // Constants contribute nothing
        }

        // Check left term
        CollectUnbound(leftValue);
        // Check right term
        CollectUnbound(rightValue);

        // Decision logic with negation support
        if (hasUnboundWriter)
        {
            // Contains unbound writer(s) → FAIL (cannot determine equality)
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }
        else if (unboundReaders.Count > 0)
        {
            // Contains unbound readers → SUSPEND
            return _Step.Jump(_suspendAndFailMulti(cx, unboundReaders, pc));
        }
        else
        {
            // Both terms are ground - dereference fully and compare
            var (leftDeref, _) = _dereferenceWithTracking(leftValue, cx);
            var (rightDeref, _) = _dereferenceWithTracking(rightValue, cx);

            var areEqual = _termsEqual(leftDeref, rightDeref, cx);

            var success = areEqual;
            if (op.Negated)
            {
                success = !success;
            }

            if (success)
            {
                return _Step.Advance();
            }
            else
            {
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
        }
    }

    private _Step ExecGuard(RunnerContext cx, Guard op)
    {
        var pc = cx.Pc;
        // Execute guard predicate with three-valued semantics
        // Guards can SUCCESS (continue), FAIL (try next clause), or SUSPEND (add to Si)
        var predicateName = op.ProcedureLabel; // Actually the predicate name (e.g., '<', '>')
        var arity = (int)op.Arity;

        // Extract and dereference arguments from argument registers
        var args = new List<object?>();
        var unboundReaders = new HashSet<int>();

        for (var i = 0; i < arity; i++)
        {
            object? argValue;

            // Get argument from argSlots (heterogeneous term storage)
            cx.ArgSlots.TryGetValue(i, out var arg);
            if (arg != null)
            {
                argValue = arg; // Store Term directly (VarRef, ConstTerm, or StructTerm)
            }
            // Check clauseVars for HEAD variables
            else if (cx.ClauseVars.ContainsKey(i))
            {
                argValue = cx.ClauseVars[i];
            }
            else
            {
                // No argument at this slot
                argValue = null;
            }

            // Dereference to get actual values, tracking unbound readers
            if (argValue != null)
            {
                var (derefValue, readers) = _dereferenceWithTracking(argValue, cx);
                args.Add(derefValue);
                unboundReaders.UnionWith(readers);
            }
            else
            {
                args.Add(null);
            }
        }

        // If any arguments have unbound readers, suspend
        // EXCEPTION: 'unknown' guard specifically tests for unbound - don't suspend
        if (unboundReaders.Count > 0 && predicateName != "unknown")
        {
            return _Step.Jump(_suspendAndFailMulti(cx, unboundReaders, pc));
        }

        // All arguments are ground - evaluate the guard
        var result = _evaluateGuard(predicateName, args, cx);

        // Handle guard negation: invert success/fail (suspend unchanged)
        if (op.Negated)
        {
            if (result == GuardResult.Pass)
            {
                result = GuardResult.Fail;
            }
            else if (result == GuardResult.Fail)
            {
                result = GuardResult.Pass;
            }
            // suspend stays suspend
        }

        if (result == GuardResult.Pass)
        {
            return _Step.Advance();
        }
        else
        {
            // FAIL - try next clause
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }
    }

    private _Step ExecGuardFail(RunnerContext cx, GuardFail op)
        => _Step.Advance();

    private _Step ExecGuardNeedReader(RunnerContext cx, GuardNeedReader op)
    {
        var pc = cx.Pc;
        var readerAddr = (int)op.ReaderId;
        // Check sigmaHat first for tentative bindings, then use isReaderBound for imported reader support
        var writerAddr = cx.Rt.Heap.TryWriterForReader(readerAddr);
        var bound = cx.SigmaHat.ContainsKey(readerAddr) ||
                    (writerAddr != null && cx.SigmaHat.ContainsKey(writerAddr.Value)) ||
                    cx.Rt.Heap.IsReaderBound(readerAddr);
        // Faithful translation of Dart:
        //   if (!bound) pc = _suspendAndFail(cx, readerAddr, pc); continue;
        //   pc++; continue;   // <- dead in Dart; the unconditional `continue` skips it
        if (!bound) return _Step.Jump(_suspendAndFail(cx, readerAddr, pc));
        return _Step.Jump(pc);
    }

    private _Step ExecGuardNeedReaderArg(RunnerContext cx, GuardNeedReaderArg op)
    {
        var pc = cx.Pc;
        var arg = cx.Env.Arg((int)op.Slot);
        if (arg is VarRef vArg && cx.Rt.Heap.IsReader(vArg.Addr))
        {
            // Check sigmaHat first for tentative bindings, then use isReaderBound for imported reader support
            var writerAddr = cx.Rt.Heap.TryWriterForReader(vArg.Addr);
            var bound = cx.SigmaHat.ContainsKey(vArg.Addr) ||
                        (writerAddr != null && cx.SigmaHat.ContainsKey(writerAddr.Value)) ||
                        cx.Rt.Heap.IsReaderBound(vArg.Addr);
            // Faithful translation of Dart (the `continue` is inside this if-block):
            //   if (!bound) pc = _suspendAndFail(cx, arg.addr, pc); continue;
            if (!bound) return _Step.Jump(_suspendAndFail(cx, vArg.Addr, pc));
            return _Step.Jump(pc);
        }
        return _Step.Advance();
    }

    private _Step ExecHalt(RunnerContext cx, Halt op)
        => _Step.Stop(RunResult.Terminated);

    private _Step ExecHeadBindWriter(RunnerContext cx, HeadBindWriter op)
    {
        // Mark writer as involved (no value binding for legacy opcode)
        cx.SigmaHat[(int)op.WriterId] = null;
        return _Step.Advance();
    }

    private _Step ExecHeadBindWriterArg(RunnerContext cx, HeadBindWriterArg op)
    {
        var arg = cx.Env.Arg((int)op.Slot);
        if (arg is VarRef vArg && cx.Rt.Heap.IsWriter(vArg.Addr))
        {
            cx.SigmaHat[vArg.Addr] = null;
        }
        return _Step.Advance();
    }

    private _Step ExecHeadConstant(RunnerContext cx, HeadConstant op)
    {
        var pc = cx.Pc;
        var arg = _getArg(cx, (int)op.ArgSlot);
        if (arg == null) return _Step.Advance(); // No argument at this slot

        if (arg is VarRef wRef && cx.Rt.Heap.IsWriter(wRef.Addr))
        {
            // Writer VarRef: check if already bound, else record tentative binding in σ̂w
            if (cx.Rt.Heap.IsWriterBound(wRef.Addr))
            {
                // Already bound - check if value matches
                object? value = cx.Rt.Heap.ValueOfWriter(wRef.Addr);

                // Dereference VarRef chains to get actual value
                while (value is VarRef chain)
                {
                    if (cx.Rt.Heap.IsReader(chain.Addr))
                    {
                        if (cx.Rt.Heap.IsReaderBound(chain.Addr))
                        {
                            var readerValue = cx.Rt.Heap.GetReaderValue(chain.Addr);
                            if (readerValue != null)
                            {
                                value = readerValue;
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (cx.Rt.Heap.IsWriterBound(chain.Addr))
                        {
                            value = cx.Rt.Heap.ValueOfWriter(chain.Addr);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (value is VarRef unbound)
                {
                    // Unbound after dereferencing
                    if (cx.Rt.Heap.IsReader(unbound.Addr))
                    {
                        // Unbound reader - add to Si and continue (two-phase)
                        cx.Si.Add(unbound.Addr);
                        return _Step.Advance();
                    }
                    else
                    {
                        // Unbound writer - create tentative binding
                        cx.SigmaHat[wRef.Addr] = new ConstTerm(op.Value);
                    }
                }
                else if (value is ConstTerm ct && !NumEquals(ct.Value, op.Value))
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
                else if (value is StructTerm)
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else
            {
                // Unbound writer - record tentative binding in σ̂w
                cx.SigmaHat[wRef.Addr] = new ConstTerm(op.Value);
            }
        }
        else if (arg is VarRef rRef && cx.Rt.Heap.IsReader(rRef.Addr))
        {
            // Reader VarRef: use derefAddr to handle both local and imported readers
            var deref = cx.Rt.Heap.DerefAddr(rRef.Addr);
            if (deref is GlpRuntime.Multiagent.VariableEntry || deref is VarRef)
            {
                // Unbound (imported or local) - suspend
                var suspendOnVar = _finalUnboundVar(cx, rRef.Addr);
                cx.Si.Add(suspendOnVar);
                return _Step.Advance();
            }
            else if (deref is Term term)
            {
                // Bound - check if value matches constant
                var value = term;
                if (value is ConstTerm ct && !NumEquals(ct.Value, op.Value))
                {
                    // Value mismatch - soft fail to next clause
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
                else if (value is StructTerm && op.Value != null)
                {
                    // Structure doesn't match constant - soft fail
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
                else if (value is StructTerm && op.Value == null)
                {
                    // Structure doesn't match null [] - soft fail
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
                // else: values match or value is compatible, continue
            }
        }
        else
        {
            // Ground: check if value matches
            // TODO: implement proper ground term matching
        }
        return _Step.Advance();
    }

    private _Step ExecHeadList(RunnerContext cx, HeadList op)
    {
        var pc = cx.Pc;
        // Match list structure [H|T] with argument
        // Equivalent to HeadStructure('[|]', 2, op.argSlot)
        var arg = _getArg(cx, (int)op.ArgSlot);
        if (arg == null) return _Step.Advance(); // No argument at this slot

        // Per spec v2.16.3 Section 12.0.1: Handle VarRef pointing to ValueTag cell
        if (arg is VarRef vArg && cx.Rt.Heap.IsValue(vArg.Addr))
        {
            var value = cx.Rt.Heap.GetValue(vArg.Addr);
            // Check for list structure (functor '.' or '[|]')
            if (value is StructTerm st && (st.Functor == "." || st.Functor == "[|]") && st.Args.Count == 2)
            {
                cx.CurrentStructure = value;
                cx.S = 0;
                cx.Mode = UnifyMode.Read;
                return _Step.Advance();
            }
            else
            {
                // Not a list structure - fail
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
        }

        if (arg is VarRef wArg && cx.Rt.Heap.IsWriter(wArg.Addr))
        {
            // Writer: create tentative structure in σ̂w
            if (cx.Rt.Heap.IsFullyBound(wArg.Addr))
            {
                // Already bound - check if it's a list structure
                var value = cx.Rt.Heap.GetValue(wArg.Addr);
                if (value is StructTerm st && st.Functor == "[|]" && st.Args.Count == 2)
                {
                    cx.CurrentStructure = value;
                    cx.S = 0;
                    cx.Mode = UnifyMode.Read;
                }
                else
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else
            {
                // Unbound writer - create tentative structure
                var struc = new StructTerm("[|]", new List<Term>());
                cx.SigmaHat[wArg.Addr] = struc;
                cx.CurrentStructure = struc;
                cx.S = 0;
                cx.Mode = UnifyMode.Write;
            }
        }
        else if (arg is VarRef rArg && cx.Rt.Heap.IsReader(rArg.Addr))
        {
            // Reader: check if bound, else add to Si (two-phase)
            // Use abstraction methods that work for both local and imported readers
            var bound = cx.Rt.Heap.IsReaderBound(rArg.Addr);
            var value = bound ? cx.Rt.Heap.GetReaderValue(rArg.Addr) : null;

            if (!bound)
            {
                // Unbound reader - add to Si and continue (two-phase)
                var suspendOnVar = _finalUnboundVar(cx, rArg.Addr);
                cx.Si.Add(suspendOnVar);
                return _Step.Advance();
            }
            else
            {
                // Bound reader - check if it's a list structure
                if (value is StructTerm st && st.Functor == "[|]" && st.Args.Count == 2)
                {
                    cx.CurrentStructure = value;
                    cx.S = 0;
                    cx.Mode = UnifyMode.Read;
                }
                else
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
        }
        return _Step.Advance();
    }

    private _Step ExecHeadNil(RunnerContext cx, HeadNil op)
    {
        var pc = cx.Pc;
        // Match empty list [] with argument or clause variable
        // Check if argSlot refers to a clause variable (for nested structures) or argument register
        bool isClauseVar = op.ArgSlot >= 10;
        var arg = isClauseVar ? null : _getArg(cx, (int)op.ArgSlot);

        // For clause variables, get the value from clauseVars
        if (isClauseVar)
        {
            cx.ClauseVars.TryGetValue((int)op.ArgSlot, out var clauseVarValue);
            if (clauseVarValue == null)
            {
                // Unbound clause variable - soft fail
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }

            // Check if the value is [] (empty list)
            if (clauseVarValue is ConstTerm cvConst)
            {
                if (Equals(cvConst.Value, "nil"))
                {
                    // Match!
                    return _Step.Advance();
                }
                else
                {
                    // Non-empty constant
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else if (clauseVarValue is StructTerm)
            {
                // Structure (non-empty list) doesn't match []
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
            else if (clauseVarValue is VarRef cvRef)
            {
                // VarRef stored in clauseVars - extract addr and handle
                // Use abstraction methods that work for both local and imported readers
                var addr = cvRef.Addr;
                if (cx.Rt.Heap.IsWriter(addr))
                {
                    // Writer VarRef
                    if (cx.Rt.Heap.IsFullyBound(addr))
                    {
                        var value = cx.Rt.Heap.GetValue(addr);
                        if (value is ConstTerm ct && Equals(ct.Value, "nil"))
                        {
                            return _Step.Advance();
                        }
                        else
                        {
                            _softFailToNextClause(cx, pc);
                            return _Step.Jump(_findNextClauseTry(pc));
                        }
                    }
                    else
                    {
                        // Unbound writer - bind to nil in σ̂w
                        cx.SigmaHat[addr] = new ConstTerm("nil");
                        return _Step.Advance();
                    }
                }
                else
                {
                    // Reader VarRef - check if bound
                    if (cx.Rt.Heap.IsReaderBound(addr))
                    {
                        var value = cx.Rt.Heap.GetReaderValue(addr);
                        if (value is ConstTerm ct && Equals(ct.Value, "nil"))
                        {
                            return _Step.Advance();
                        }
                        else
                        {
                            _softFailToNextClause(cx, pc);
                            return _Step.Jump(_findNextClauseTry(pc));
                        }
                    }
                    else
                    {
                        // Unbound reader - add to Si (suspend)
                        var suspendOnVar = _finalUnboundVar(cx, addr);
                        cx.Si.Add(suspendOnVar);
                        return _Step.Advance();
                    }
                }
            }
            else if (clauseVarValue is int writerAddrCv)
            {
                // Writer addr - check if bound
                if (cx.Rt.Heap.IsFullyBound(writerAddrCv))
                {
                    var value = cx.Rt.Heap.GetValue(writerAddrCv);
                    if (value is ConstTerm ct && Equals(ct.Value, "nil"))
                    {
                        return _Step.Advance();
                    }
                    else
                    {
                        _softFailToNextClause(cx, pc);
                        return _Step.Jump(_findNextClauseTry(pc));
                    }
                }
                else
                {
                    // Unbound writer - enter WRITE mode to bind to []
                    cx.SigmaHat[writerAddrCv] = new ConstTerm("nil");
                    return _Step.Advance();
                }
            }

            // Unexpected clauseVar type
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        // Regular argument handling
        if (arg == null) return _Step.Advance(); // No argument at this slot

        // Per spec v2.16.3 Section 12.0.1: All arguments are VarRefs
        // Handle VarRef pointing to ValueTag cell (heap-stored constant/structure)
        if (arg is VarRef vArg && cx.Rt.Heap.IsValue(vArg.Addr))
        {
            var value = cx.Rt.Heap.GetValue(vArg.Addr);
            if (value is ConstTerm ct && Equals(ct.Value, "nil"))
            {
                // Match! Empty list
                return _Step.Advance();
            }
            else
            {
                // Value doesn't match [] - fail
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
        }

        // Note: getValue() dereferences automatically per FCP AM semantics
        if (arg is VarRef wArg && cx.Rt.Heap.IsWriter(wArg.Addr))
        {
            // Writer: check if already bound, else record tentative binding in σ̂w
            if (cx.Rt.Heap.IsFullyBound(wArg.Addr))
            {
                // Already bound - check if value matches []
                var value = cx.Rt.Heap.GetValue(wArg.Addr);
                if (value is ConstTerm ct && !Equals(ct.Value, "nil"))
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
                else if (value is StructTerm)
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else
            {
                // Unbound writer - record tentative binding in σ̂w
                cx.SigmaHat[wArg.Addr] = new ConstTerm("nil");
            }
        }
        else if (arg is VarRef rArg && cx.Rt.Heap.IsReader(rArg.Addr))
        {
            // Reader: check if bound, else add to Si (two-phase)
            // Use abstraction methods that work for both local and imported readers
            var bound = cx.Rt.Heap.IsReaderBound(rArg.Addr);
            var value = bound ? cx.Rt.Heap.GetReaderValue(rArg.Addr) : null;

            if (!bound)
            {
                // Unbound reader - add to Si and continue (two-phase)
                var suspendOnVar = _finalUnboundVar(cx, rArg.Addr);
                cx.Si.Add(suspendOnVar);
                return _Step.Advance();
            }
            else
            {
                // Bound reader - check if value matches []
                if (value is ConstTerm ct && Equals(ct.Value, "nil"))
                {
                    // Match! Empty list
                }
                else if (value is StructTerm)
                {
                    // Structure doesn't match []
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
                else
                {
                    // Non-empty constant doesn't match []
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
        }
        return _Step.Advance();
    }

    private _Step ExecHeadStructure(RunnerContext cx, HeadStructure op)
    {
        var pc = cx.Pc;
        // Check if argSlot refers to a clause variable (for nested structures) or argument register
        // Clause variables are used when matching extracted nested structures (argSlot >= 10 by convention)
        bool isClauseVar = op.ArgSlot >= 10;
        var arg = isClauseVar ? null : _getArg(cx, (int)op.ArgSlot);

        if (!isClauseVar && arg == null)
        {
            // No argument - soft fail to next clause
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        // For clause variables, get the value from clauseVars
        if (isClauseVar)
        {
            cx.ClauseVars.TryGetValue((int)op.ArgSlot, out var clauseVarValue);
            if (clauseVarValue == null)
            {
                // Unbound clause variable - soft fail
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }

            // If clauseVarValue is a WriterTerm or ReaderId, treat it as if it came from argument
            if (clauseVarValue is int wid)
            {
                // It's a writer ID - check if bound
                if (cx.Rt.Heap.IsWriterBound(wid))
                {
                    // Writer is bound - check if it matches
                    var value = cx.Rt.Heap.ValueOfWriter(wid);
                    if (value is StructTerm st && st.Functor == op.Functor && st.Args.Count == op.Arity)
                    {
                        cx.CurrentStructure = value;
                        cx.Mode = UnifyMode.Read;
                        cx.S = 0;
                        return _Step.Advance();
                    }
                    // Bound but doesn't match
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
                else
                {
                    // Writer is unbound - enter WRITE mode to create structure
                    var struc = new _TentativeStruct(op.Functor, (int)op.Arity, NullArgs((int)op.Arity));
                    cx.SigmaHat[wid] = struc;
                    cx.CurrentStructure = struc;
                    cx.Mode = UnifyMode.Write;
                    cx.S = 0;
                    return _Step.Advance();
                }
            }
            else if (clauseVarValue is VarRef cvWriter && cx.Rt.Heap.IsWriter(cvWriter.Addr))
            {
                // VarRef writer - check if bound, or create tentative structure
                var wid2 = cvWriter.Addr;
                if (cx.Rt.Heap.IsWriterBound(wid2))
                {
                    // Writer is bound - check if it matches
                    var value = cx.Rt.Heap.ValueOfWriter(wid2);
                    if (value is StructTerm st && st.Functor == op.Functor && st.Args.Count == op.Arity)
                    {
                        cx.CurrentStructure = value;
                        cx.Mode = UnifyMode.Read;
                        cx.S = 0;
                        return _Step.Advance();
                    }
                    // Bound but doesn't match
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
                else
                {
                    // FIX: Unbound writer - create tentative structure in σ̂w
                    var struc = new _TentativeStruct(op.Functor, (int)op.Arity, NullArgs((int)op.Arity));
                    cx.SigmaHat[wid2] = struc;
                    cx.CurrentStructure = struc;
                    cx.Mode = UnifyMode.Write;
                    cx.S = 0;
                    return _Step.Advance();
                }
            }
            else if (clauseVarValue is VarRef cvReader && cx.Rt.Heap.IsReader(cvReader.Addr))
            {
                // VarRef reader - dereference and check if bound to matching structure
                // Use abstraction methods that work for both local and imported readers
                var rid = cvReader.Addr;
                var bound = cx.Rt.Heap.IsReaderBound(rid);
                if (!bound)
                {
                    // Unbound reader - add to Si and continue (two-phase)
                    cx.Si.Add(rid);
                    return _Step.Advance();
                }
                // Bound reader - get value and check structure
                var rawValue = cx.Rt.Heap.GetReaderValue(rid);
                if (rawValue == null)
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
                var derefValue = cx.Rt.Heap.Dereference(rawValue);
                if (derefValue is StructTerm st2 && st2.Functor == op.Functor && st2.Args.Count == op.Arity)
                {
                    // Match!
                    cx.CurrentStructure = derefValue;
                    cx.Mode = UnifyMode.Read;
                    cx.S = 0;
                    return _Step.Advance();
                }
                else
                {
                    // No match
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else if (clauseVarValue is StructTerm cvStruct)
            {
                // Direct structure value (from dereferencing a bound reader)
                if (cvStruct.Functor == op.Functor && cvStruct.Args.Count == op.Arity)
                {
                    cx.CurrentStructure = cvStruct;
                    cx.Mode = UnifyMode.Read;
                    cx.S = 0;
                    return _Step.Advance();
                }
                else
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else if (clauseVarValue is ConstTerm)
            {
                // Constant value (e.g., [] or atom) - cannot match structure
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }

            // Unexpected clauseVar type
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        if (arg is VarRef wArg && cx.Rt.Heap.IsWriter(wArg.Addr))
        {
            // Writer VarRef: check if writer is already bound
            if (cx.Rt.Heap.IsWriterBound(wArg.Addr))
            {
                // Already bound - check if matches structure
                object? value = cx.Rt.Heap.ValueOfWriter(wArg.Addr);

                // Dereference VarRef chains to get actual value
                while (value is VarRef chain)
                {
                    if (cx.Rt.Heap.IsReader(chain.Addr))
                    {
                        if (cx.Rt.Heap.IsReaderBound(chain.Addr))
                        {
                            var readerValue = cx.Rt.Heap.GetReaderValue(chain.Addr);
                            if (readerValue != null)
                            {
                                value = readerValue;
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (cx.Rt.Heap.IsWriterBound(chain.Addr))
                        {
                            value = cx.Rt.Heap.ValueOfWriter(chain.Addr);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (value is VarRef unbound)
                {
                    // Unbound after dereferencing
                    if (cx.Rt.Heap.IsReader(unbound.Addr))
                    {
                        // Unbound reader - add to Si and continue (two-phase)
                        cx.Si.Add(unbound.Addr);
                        return _Step.Advance();
                    }
                    else
                    {
                        // Unbound writer - enter WRITE mode
                        var struc = new _TentativeStruct(op.Functor, (int)op.Arity, NullArgs((int)op.Arity));
                        cx.SigmaHat[wArg.Addr] = struc;
                        cx.CurrentStructure = struc;
                        cx.Mode = UnifyMode.Write;
                        cx.S = 0;
                        return _Step.Advance();
                    }
                }
                else if (value is StructTerm st && st.Functor == op.Functor && st.Args.Count == op.Arity)
                {
                    // MATCH! Enter READ mode
                    cx.CurrentStructure = value;
                    cx.Mode = UnifyMode.Read;
                    cx.S = 0;
                    return _Step.Advance();
                }
                else
                {
                    // No match - soft fail
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            // Unbound writer - WRITE mode: create tentative structure for writer
            var struc2 = new _TentativeStruct(op.Functor, (int)op.Arity, NullArgs((int)op.Arity));
            cx.SigmaHat[wArg.Addr] = struc2;
            cx.CurrentStructure = struc2;
            cx.Mode = UnifyMode.Write;
            cx.S = 0; // Start at first arg
            return _Step.Advance();
        }

        if (arg is VarRef rArg && cx.Rt.Heap.IsReader(rArg.Addr))
        {
            // Reader VarRef: check if bound and has matching structure
            // Use abstraction methods that work for both local and imported readers
            if (!cx.Rt.Heap.IsReaderBound(rArg.Addr))
            {
                // Unbound reader (local or imported) - add to Si and continue (two-phase)
                var suspendOnVar = _finalUnboundVar(cx, rArg.Addr);
                cx.Si.Add(suspendOnVar);
                return _Step.Advance();
            }

            // Bound reader - dereference fully and check if it's a matching structure
            var rawValue = cx.Rt.Heap.GetReaderValue(rArg.Addr);
            if (rawValue == null)
            {
                // Null value - should not happen for bound reader
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
            // Dereference recursively in case value is a VarRef chain
            var value = cx.Rt.Heap.Dereference(rawValue);
            if (value is StructTerm st && st.Functor == op.Functor && st.Args.Count == op.Arity)
            {
                // Matching structure - enter READ mode
                cx.CurrentStructure = value;
                cx.Mode = UnifyMode.Read;
                cx.S = 0;
                return _Step.Advance();
            }
            else
            {
                // Non-matching structure or not a structure - soft fail
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
        }

        // Per spec v2.16.3 Section 12.0.1: Handle VarRef pointing to ValueTag cell
        if (arg is VarRef vArg && cx.Rt.Heap.IsValue(vArg.Addr))
        {
            var value = cx.Rt.Heap.GetValue(vArg.Addr);
            if (value is StructTerm st && st.Functor == op.Functor && st.Args.Count == op.Arity)
            {
                // Match! Enter READ mode
                cx.CurrentStructure = value;
                cx.Mode = UnifyMode.Read;
                cx.S = 0;
                return _Step.Advance();
            }
            else
            {
                // No match - soft fail
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
        }

        // Per spec v2.16.3: All args should be VarRefs, handled above
        // This is unreachable if assertion in _getArg holds
        throw new InvalidOperationException($"HeadStructure: unexpected argument type {arg?.GetType()}");
    }

    /// <summary>Build a list of <paramref name="arity"/> null args (mirrors Dart List.filled(arity, null)).</summary>
    private static List<object?> NullArgs(int arity)
    {
        var list = new List<object?>(arity);
        for (var i = 0; i < arity; i++) list.Add(null);
        return list;
    }

    private _Step ExecKnown(RunnerContext cx, Known op)
    {
        var pc = cx.Pc;
        // known(X): Succeeds if X is not an unbound variable
        // ~known(X): Succeeds if X IS an unbound variable (equivalent to unknown/1)
        cx.ClauseVars.TryGetValue((int)op.VarIndex, out var value);
        if (value == null)
        {
            // Variable doesn't exist - fail (even for negated)
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        // Check if value is known
        // NOTE: Must check BOTH sigmaHat (tentative bindings) AND heap bindings
        var isKnown = false;
        int? unboundReader = null;
        var isUnboundWriter = false;

        if (value is int valueInt)
        {
            // Could be writer addr or reader addr - check sigmaHat first
            if (cx.SigmaHat.ContainsKey(valueInt))
            {
                isKnown = true; // Has tentative binding
            }
            else if (cx.Rt.Heap.IsWriter(valueInt))
            {
                // It's a writer addr - check if bound
                if (cx.Rt.Heap.IsFullyBound(valueInt))
                {
                    isKnown = true;
                }
                else
                {
                    isUnboundWriter = true;
                }
            }
            else
            {
                // It's a reader addr - use isReaderBound for imported reader support
                var writerAddr = cx.Rt.Heap.TryWriterForReader(valueInt);
                if (writerAddr != null && cx.SigmaHat.ContainsKey(writerAddr.Value))
                {
                    isKnown = true; // Writer has tentative binding
                }
                else if (cx.Rt.Heap.IsReaderBound(valueInt))
                {
                    isKnown = true;
                }
                else
                {
                    // Unbound reader - could become known later
                    unboundReader = valueInt;
                }
            }
        }
        else if (value is VarRef wvalue && cx.Rt.Heap.IsWriter(wvalue.Addr))
        {
            // Writer - check sigmaHat first, then heap
            if (cx.SigmaHat.ContainsKey(wvalue.Addr))
            {
                isKnown = true;
            }
            else if (cx.Rt.Heap.IsFullyBound(wvalue.Addr))
            {
                isKnown = true;
            }
            else
            {
                isUnboundWriter = true;
            }
        }
        else if (value is VarRef rvalue && cx.Rt.Heap.IsReader(rvalue.Addr))
        {
            // Reader - check sigmaHat first, then heap
            var readerAddr = rvalue.Addr;
            if (cx.SigmaHat.ContainsKey(readerAddr))
            {
                isKnown = true;
            }
            else
            {
                // Use tryWriterForReader for imported reader support
                var writerAddr = cx.Rt.Heap.TryWriterForReader(readerAddr);
                if (writerAddr != null && cx.SigmaHat.ContainsKey(writerAddr.Value))
                {
                    isKnown = true;
                }
                else if (cx.Rt.Heap.IsReaderBound(readerAddr))
                {
                    isKnown = true;
                }
                else
                {
                    unboundReader = readerAddr;
                }
            }
        }
        else
        {
            // Constant or structure - always known
            isKnown = true;
        }

        // Decision logic with negation support
        if (op.Negated)
        {
            // ~known(X) semantics
            if (isUnboundWriter)
            {
                // Variable is unbound writer → definitely unknown → SUCCEED
                return _Step.Advance();
            }
            else if (unboundReader != null)
            {
                // Variable is unbound reader → might become known → SUSPEND
                return _Step.Jump(_suspendAndFail(cx, unboundReader.Value, pc));
            }
            else
            {
                // Variable is known → FAIL
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
        }
        else
        {
            // known(X) semantics (original)
            if (isKnown)
            {
                // Variable is known - succeed
                return _Step.Advance();
            }
            else if (unboundReader != null)
            {
                // Variable is unbound reader - could become known later, add to Si
                return _Step.Jump(_suspendAndFail(cx, unboundReader.Value, pc));
            }
            else
            {
                // Variable is unbound writer - fail
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
        }
    }

    private _Step ExecLabel(RunnerContext cx, Label op)
        => _Step.Advance();

    private _Step ExecNoMoreClauses(RunnerContext cx, NoMoreClauses op)
    {
        // no_more_clauses: All clauses exhausted (spec 2.5)
        // If U non-empty: suspend; otherwise: fail definitively
        if (cx.U.Count > 0)
        {
            // feature-020 equiv-trace: the goal suspends on the readers in U.
            if (EquivTrace.Enabled)
            {
                EquivTrace.Unify("suspend", cx.U);
                foreach (var r in cx.U) EquivTrace.Suspend(r, cx.GoalId);
            }
            cx.Rt.SuspendGoalFCP(goalId: cx.GoalId, kappa: cx.Kappa, readerVarIds: cx.U);
            cx.U.Clear();
            cx.InBody = false;
            return _Step.Stop(RunResult.Suspended);
        }
        // U is empty - all clauses failed definitively (no suspension)
        EquivTrace.Unify("fail");  // feature-020 (no-op unless enabled)
        cx.InBody = false;
        // According to spec, failed goals should be added to F set.
        // For now, just terminate - the goal is done (failed).
        return _Step.Stop(RunResult.Terminated);
    }

    private _Step ExecNoReaders(RunnerContext cx, NoReaders op)
    {
        var pc = cx.Pc;
        // no_readers(X): Succeeds if X contains no readers (only ground terms or writers)
        // ~no_readers(X): Succeeds if X DOES contain readers
        cx.ClauseVars.TryGetValue((int)op.VarIndex, out var value);

        if (value == null)
        {
            // Variable doesn't exist - for no_readers, this means no readers → succeed
            // For ~no_readers, no readers means fail
            if (op.Negated)
            {
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
            else
            {
                return _Step.Advance();
            }
        }

        // Collect all readers in the term (we need to suspend on them)
        // Unlike ground, we don't care about writers - writers are fine
        var readers = new HashSet<int>();
        var visited = new HashSet<int>();

        void CollectReaders(object? term)
        {
            if (term is VarRef rterm && cx.Rt.Heap.IsReader(rterm.Addr))
            {
                var readerAddr = rterm.Addr;
                if (visited.Contains(readerAddr)) return;
                visited.Add(readerAddr);
                // Check if reader is bound - if so, traverse its value
                if (cx.SigmaHat.TryGetValue(readerAddr, out var sigmaBinding) && sigmaBinding != null)
                {
                    CollectReaders(sigmaBinding);
                }
                else if (cx.Rt.Heap.IsReaderBound(readerAddr))
                {
                    CollectReaders(cx.Rt.Heap.GetReaderValue(readerAddr));
                }
                else
                {
                    // Unbound reader - add to suspension set
                    readers.Add(readerAddr);
                }
            }
            else if (term is VarRef wterm && cx.Rt.Heap.IsWriter(wterm.Addr))
            {
                // Writers are OK for no_readers - they can be sent to external systems
                // But we need to traverse their bindings to check for readers inside
                var writerAddr = wterm.Addr;
                if (visited.Contains(writerAddr)) return;
                visited.Add(writerAddr);
                if (cx.SigmaHat.TryGetValue(writerAddr, out var sigmaBinding) && sigmaBinding != null)
                {
                    CollectReaders(sigmaBinding);
                }
                else if (cx.Rt.Heap.IsFullyBound(writerAddr))
                {
                    CollectReaders(cx.Rt.Heap.GetValue(writerAddr));
                }
                // Unbound writer is fine - no readers contributed
            }
            else if (term is StructTerm st)
            {
                foreach (var arg in st.Args) CollectReaders(arg);
            }
            else if (term is _TentativeStruct ts)
            {
                foreach (var arg in ts.Args) CollectReaders(arg);
            }
            // Constants contribute no readers
        }

        // Dereference the clause variable and collect readers
        if (value is int valueInt)
        {
            if (cx.SigmaHat.TryGetValue(valueInt, out var sigmaBinding) && sigmaBinding != null)
            {
                CollectReaders(sigmaBinding);
            }
            else if (cx.Rt.Heap.IsWriter(valueInt))
            {
                if (cx.Rt.Heap.IsFullyBound(valueInt))
                {
                    CollectReaders(cx.Rt.Heap.GetValue(valueInt));
                }
                // Unbound writer is fine
            }
            else
            {
                // Reader address
                if (visited.Contains(valueInt))
                {
                    // Already visited
                }
                else if (cx.Rt.Heap.IsReaderBound(valueInt))
                {
                    CollectReaders(cx.Rt.Heap.GetReaderValue(valueInt));
                }
                else
                {
                    readers.Add(valueInt);
                }
            }
        }
        else
        {
            CollectReaders(value);
        }

        // Decision logic:
        if (op.Negated)
        {
            // ~no_readers(X) - succeeds if X HAS readers
            if (readers.Count > 0)
            {
                // Has readers → SUCCEED
                return _Step.Advance();
            }
            else
            {
                // No readers → fail
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
        }
        else
        {
            // no_readers(X) semantics
            if (readers.Count == 0)
            {
                // No readers found → SUCCEED
                return _Step.Advance();
            }
            else
            {
                // Has readers → SUSPEND (never fails)
                return _Step.Jump(_suspendAndFailMulti(cx, readers, pc));
            }
        }
    }

    private _Step ExecNop(RunnerContext cx, Nop op)
        => _Step.Advance();

    private _Step ExecOtherwise(RunnerContext cx, Otherwise op)
    {
        var pc = cx.Pc;
        // Otherwise guard: succeeds if Si is empty (all previous clauses failed, not suspended).
        // Otherwise succeeds only if all previous clauses definitively failed.
        // If any clause suspended (U non-empty), then otherwise should also suspend.
        if (cx.U.Count > 0)
        {
            // Previous clauses suspended, so this clause also suspends.
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }
        // U and Si both empty - all previous clauses definitely failed, so succeed.
        return _Step.Advance();
    }

    private _Step ExecPop(RunnerContext cx, Pop op)
    {
        // Pop: Restore structure processing state (FCP AM semantics)
        var state = (_StructureState)cx.ClauseVars[(int)op.RegIndex]!;

        // FCP AM: Pop saves the built nested structure to register.
        // This makes it available for subsequent UnifyWriter/UnifyVariable.
        cx.ClauseVars[(int)op.RegIndex] = cx.CurrentStructure;

        // Restore parent context
        cx.S = state.S;
        cx.Mode = state.Mode;
        cx.CurrentStructure = state.CurrentStructure;
        return _Step.Advance();
    }

    private _Step ExecProceed(RunnerContext cx, Proceed op)
    {
        // Call reduction callback if trace is on
        if (cx.OnReduction != null && cx.GoalHead != null)
        {
            var body = cx.SpawnedGoals.Count == 0 ? "true" : string.Join(", ", cx.SpawnedGoals);
            cx.OnReduction!(cx.GoalId, cx.ReformatHead(), body);
        }
        // Complete current procedure - terminate execution
        return _Step.Stop(RunResult.Terminated);
    }

    private _Step ExecPush(RunnerContext cx, Push op)
    {
        // Push: Save structure processing state
        cx.ClauseVars[(int)op.RegIndex] = new _StructureState(cx.S, cx.Mode, cx.CurrentStructure);
        return _Step.Advance();
    }

    private _Step ExecPutBoundConst(RunnerContext cx, PutBoundConst op)
    {
        // Put a variable bound to a constant value
        // Used for passing constants as arguments in queries
        var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
        cx.Rt.Heap.BindWriterConst(writerAddr, op.Value);
        cx.ArgSlots[(int)op.ArgSlot] = new VarRef(readerAddr);
        return _Step.Advance();
    }

    private _Step ExecPutBoundNil(RunnerContext cx, PutBoundNil op)
    {
        // Put a variable bound to 'nil'
        // Used for passing empty lists as arguments in queries
        var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
        cx.Rt.Heap.BindWriterConst(writerAddr, "nil");
        cx.ArgSlots[(int)op.ArgSlot] = new VarRef(readerAddr);
        return _Step.Advance();
    }

    private _Step ExecPutConstant(RunnerContext cx, PutConstant op)
    {
        // Create fresh variable, bind to constant, store reader VarRef in argSlot
        // Per baseline behavior: constants are stored as VarRefs to bound variables
        var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
        cx.Rt.Heap.BindWriterConst(writerAddr, op.Value);
        cx.ArgSlots[(int)op.ArgSlot] = new VarRef(readerAddr);
        return _Step.Advance();
    }

    private _Step ExecPutList(RunnerContext cx, PutList op)
    {
        // Begin list construction in argument register
        // Equivalent to PutStructure('[|]', 2, op.argSlot)
        if (cx.InBody)
        {
            // Store target writer addr from environment
            var arg = cx.Env.Arg((int)op.ArgSlot);
            int? targetWriterAddr = (arg is VarRef avr && cx.Rt.Heap.IsWriter(avr.Addr)) ? avr.Addr : (int?)null;
            if (targetWriterAddr == null)
            {
                // WARNING: PutList argSlot has no writer in environment
                return _Step.Advance();
            }

            // Store the writer addr in context for later binding
            cx.ClauseVars[-1] = targetWriterAddr.Value; // Use -1 as special marker for structure binding

            // Create list structure [H|T] with placeholder args (will be filled by Set* instructions)
            var structArgs = new List<Term>(2) { new ConstTerm(null), new ConstTerm(null) }; // Lists have arity 2
            cx.CurrentStructure = new StructTerm("[|]", structArgs);
            cx.S = 0; // Start at first argument position
            cx.Mode = UnifyMode.Write;
        }
        return _Step.Advance();
    }

    private _Step ExecPutNil(RunnerContext cx, PutNil op)
    {
        if (cx.InBody)
        {
            // Place empty list [] in argument register
            // Create a fresh variable bound to [] (same as PutConstant)
            var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
            cx.Rt.Heap.BindWriterConst(writerAddr, "nil"); // [] represented as 'nil'
            cx.ArgSlots[(int)op.ArgSlot] = new VarRef(readerAddr);
        }
        return _Step.Advance();
    }

    private _Step ExecPutStructure(RunnerContext cx, PutStructure op)
    {
        if (cx.InBody)
        {
            // BODY phase: Build StructTerm with heap allocation
            // Per spec v2.16 section 7.1: Build StructTerm incrementally via set_* instructions
            // Structure will be stored in argSlots when complete

            // Create fresh variable for binding the structure
            var (writerAddr, _) = cx.Rt.Heap.AllocateVariable();

            // Handle nested structures - push parent context to stack
            if (op.ArgSlot == -1 || cx.CurrentStructure != null)
            {
                cx.ClauseVars.TryGetValue(-1, out var parentWriterId);
                cx.ParentStack.Add(new _ParentContext(
                    structure: cx.CurrentStructure,
                    s: cx.S,
                    mode: cx.Mode,
                    writerId: parentWriterId));
            }

            // Store writer address for structure binding
            cx.ClauseVars[-1] = writerAddr;

            // Store target argSlot for later (when structure is complete)
            if (op.ArgSlot >= 0 && op.ArgSlot < 10)
            {
                cx.ClauseVars[-2] = (int)op.ArgSlot; // Temporary storage of target slot
            }
            else
            {
                cx.ClauseVars[(int)op.ArgSlot] = new VarRef(writerAddr);
            }

            // Create structure with placeholder args (filled by Set* instructions)
            var structArgs = new List<Term>((int)op.Arity);
            for (var i = 0; i < (int)op.Arity; i++) structArgs.Add(new ConstTerm(null));
            cx.CurrentStructure = new StructTerm(op.Functor, structArgs);
            cx.S = 0;
            cx.Mode = UnifyMode.Write;
        }
        else
        {
            // PRE-COMMIT phase (guard argument building): Build StructTerm WITHOUT heap allocation
            // The structure is temporary, just for passing to the guard predicate
            // No writer variable binding needed - store directly in argSlots when complete

            // Remember target argSlot for when structure is complete
            cx.GuardArgSlot = (int)op.ArgSlot;

            // Create structure with placeholder args (filled by UnifyVariable/UnifyConstant)
            var structArgs = new List<Term>((int)op.Arity);
            for (var i = 0; i < (int)op.Arity; i++) structArgs.Add(new ConstTerm(null));
            cx.CurrentStructure = new StructTerm(op.Functor, structArgs);
            cx.S = 0;
            cx.Mode = UnifyMode.Write;
        }
        return _Step.Advance();
    }

    private _Step ExecRequeue(RunnerContext cx, Requeue op)
    {
        if (cx.InBody)
        {
            // Tail call - reuse current goal, jump to procedure entry
            // Get entry point for procedure
            if (!_prog.Labels.TryGetValue(op.ProcedureLabel, out var entryPc))
            {
                Console.WriteLine($"ERROR: Requeue could not find procedure label: {op.ProcedureLabel}");
                return _Step.Stop(RunResult.Terminated);
            }

            // Format requeued goal as GLP predicate with arguments
            var fmtArgs = new List<string>();
            for (var i = 0; i < 10; i++)
            {
                cx.ArgSlots.TryGetValue(i, out var term);
                if (term != null)
                {
                    // Use custom formatter if provided, otherwise fall back to static formatter
                    fmtArgs.Add(cx.TermFormatter != null
                        ? cx.TermFormatter(term, true)
                        : _formatTerm(cx.Rt, term));
                }
                else
                {
                    break;
                }
            }
            var newHeadGoalStr = fmtArgs.Count == 0 ? op.ProcedureLabel : $"{op.ProcedureLabel}({string.Join(", ", fmtArgs)})";
            cx.SpawnedGoals.Add(newHeadGoalStr);

            // Print reduction trace before tail call
            if (cx.OnReduction != null && cx.GoalHead != null)
            {
                var body = string.Join(", ", cx.SpawnedGoals);
                cx.OnReduction(cx.GoalId, cx.ReformatHead(), body);
            }

            // Update environment with new heterogeneous arguments
            cx.Env.Update(new Dictionary<int, Term>(cx.ArgSlots));

            // Clear argument registers
            cx.ArgSlots.Clear();

            // Clear spawned goals and update head for next reduction
            cx.SpawnedGoals.Clear();
            cx.GoalHead = newHeadGoalStr; // New head for next iteration

            // Reset clause state for new procedure
            cx.SigmaHat.Clear();
            // Si removed - U persists across clause attempts
            cx.U.Clear();
            cx.ClauseVars.Clear();
            cx.InBody = false;
            cx.Mode = UnifyMode.Read;
            cx.S = 0;
            cx.CurrentStructure = null;

            // Update kappa to new procedure's entry point
            // This ensures suspension/reactivation uses the correct procedure
            cx.Kappa = entryPc;

            // Jump to procedure entry
            return _Step.Jump(entryPc);
        }
        return _Step.Advance();
    }

    private _Step ExecRequireReaderArg(RunnerContext cx, RequireReaderArg op)
    {
        var arg = cx.Env.Arg((int)op.Slot);
        if (arg == null || (arg is VarRef vArg && cx.Rt.Heap.IsWriter(vArg.Addr)))
        {
            return _Step.Jump(_prog.Labels[op.FailLabel]);
        }
        return _Step.Advance();
    }

    private _Step ExecRequireWriterArg(RunnerContext cx, RequireWriterArg op)
    {
        var arg = cx.Env.Arg((int)op.Slot);
        if (arg == null || (arg is VarRef vArg && cx.Rt.Heap.IsReader(vArg.Addr)))
        {
            return _Step.Jump(_prog.Labels[op.FailLabel]);
        }
        return _Step.Advance();
    }

    private _Step ExecResetAndGoto(RunnerContext cx, ResetAndGoto op)
    {
        cx.ClearClause();
        return _Step.Jump(_prog.Labels[op.Label]);
    }

    private _Step ExecSetConstant(RunnerContext cx, SetConstant op)
    {
        if (cx.InBody && cx.Mode == UnifyMode.Write && cx.CurrentStructure is StructTerm struc)
        {
            // Store ConstTerm in current structure at position S
            var sargs = MutableArgs(struc);
            sargs[cx.S] = new ConstTerm(op.Value);
            cx.S++; // Move to next position

            // Check if structure is complete (all arguments filled)
            if (cx.S >= sargs.Count)
            {
                // Structure complete - bind the target writer (stored at clauseVars[-1])
                cx.ClauseVars.TryGetValue(-1, out var targetWriterAddr);
                // Extract int from VarRef if needed
                int? targetWriterAddrInt = targetWriterAddr is VarRef twvr ? twvr.Addr : (targetWriterAddr is int twi ? twi : (int?)null);
                if (targetWriterAddrInt != null)
                {
                    // Bind the writer to the completed structure (returns activations)
                    var acts = cx.Rt.Heap.BindWriterStruct(targetWriterAddrInt.Value, struc.Functor, sargs);
                    foreach (var a in acts)
                    {
                        cx.Rt.Gq.Enqueue(a);
                        cx.OnActivation?.Invoke(a);
                    }
                }

                // Handle parent structure restoration (nested structures) - pop from stack
                if (cx.ParentStack.Count > 0 && targetWriterAddrInt != null)
                {
                    var nestedWriterAddr = targetWriterAddrInt.Value;
                    var parent = cx.ParentStack[cx.ParentStack.Count - 1];
                    cx.ParentStack.RemoveAt(cx.ParentStack.Count - 1);
                    var parentWriterAddr = parent.WriterId;

                    if (parent.Structure is StructTerm parentStruct)
                    {
                        // Use reader address (writer + 1)
                        MutableArgs(parentStruct)[parent.S] = new VarRef(nestedWriterAddr + 1);
                    }

                    cx.CurrentStructure = parent.Structure;
                    cx.S = parent.S + 1;
                    cx.Mode = parent.Mode;
                    cx.ClauseVars[-1] = parentWriterAddr;

                    // Check if parent is now complete - and recursively complete ancestors
                    while (cx.CurrentStructure is StructTerm parentStruct2)
                    {
                        cx.ClauseVars.TryGetValue(-1, out var currentWriterAddr);
                        int? currentWriterAddrInt = currentWriterAddr is VarRef cwvr ? cwvr.Addr : (currentWriterAddr is int cwi ? cwi : (int?)null);
                        var pargs = MutableArgs(parentStruct2);

                        if (cx.S >= pargs.Count && currentWriterAddrInt != null)
                        {
                            // bindWriterStruct returns activations directly
                            var acts = cx.Rt.Heap.BindWriterStruct(currentWriterAddrInt.Value, parentStruct2.Functor, pargs);
                            foreach (var a in acts)
                            {
                                cx.Rt.Gq.Enqueue(a);
                                cx.OnActivation?.Invoke(a);
                            }

                            // Check for more ancestors
                            if (cx.ParentStack.Count > 0)
                            {
                                var ancestor = cx.ParentStack[cx.ParentStack.Count - 1];
                                cx.ParentStack.RemoveAt(cx.ParentStack.Count - 1);
                                if (ancestor.Structure is StructTerm ancestorStruct)
                                {
                                    // Use reader address (writer + 1)
                                    MutableArgs(ancestorStruct)[ancestor.S] = new VarRef(currentWriterAddrInt.Value + 1);
                                }
                                cx.CurrentStructure = ancestor.Structure;
                                cx.S = ancestor.S + 1;
                                cx.Mode = ancestor.Mode;
                                cx.ClauseVars[-1] = ancestor.WriterId;
                            }
                            else
                            {
                                // No more ancestors - store in argSlots and reset
                                if (cx.ClauseVars.TryGetValue(-2, out var pts) && pts is int parentTargetSlot && parentTargetSlot >= 0 && parentTargetSlot < 10)
                                {
                                    // Use reader address (writer + 1)
                                    cx.ArgSlots[parentTargetSlot] = new VarRef(currentWriterAddrInt.Value + 1);
                                    cx.ClauseVars.Remove(-2);
                                }
                                cx.CurrentStructure = null;
                                cx.Mode = UnifyMode.Read;
                                cx.S = 0;
                                cx.ClauseVars.Remove(-1);
                                break;
                            }
                        }
                        else
                        {
                            // Parent not complete yet, stop
                            break;
                        }
                    }
                }
                else
                {
                    // No parent - reset structure building state
                    cx.CurrentStructure = null;
                    cx.Mode = UnifyMode.Read;
                    cx.S = 0;
                    cx.ClauseVars.Remove(-1); // Clear the marker
                }
            }
        }
        return _Step.Advance();
    }

    private _Step ExecSpawn(RunnerContext cx, Spawn op)
    {
        if (cx.InBody)
        {
            // Get entry point for procedure
            var hasEntry = _prog.Labels.TryGetValue(op.ProcedureLabel, out var entryPc);

            // If procedure not found in program, check if it's a body kernel
            if (!hasEntry)
            {
                // Extract procedure name from label (may be "name" or "name/arity")
                var labelParts = op.ProcedureLabel.Split('/');
                var procName = labelParts[0];

                // Look up body kernel
                var kernel = cx.Rt.BodyKernels.Lookup(procName, op.Arity);
                if (kernel != null)
                {
                    // Execute body kernel inline
                    // Collect arguments from argSlots
                    var kArgs = new List<object?>();
                    for (var i = 0; i < (int)op.Arity; i++)
                    {
                        cx.ArgSlots.TryGetValue(i, out var slot);
                        kArgs.Add(slot);
                    }

                    // Execute kernel
                    var result = kernel(cx.Rt, kArgs);

                    if (result == BodyKernelResult.Abort)
                    {
                        Console.WriteLine($"ERROR: Body kernel {procName}/{op.Arity} aborted");
                        return _Step.Stop(RunResult.Terminated);
                    }

                    // Success - clear args and continue (no goal spawned)
                    cx.ArgSlots.Clear();
                    return _Step.Advance();
                }

                // Not a body kernel either - error
                Console.WriteLine($"ERROR: Spawn could not find procedure label: {op.ProcedureLabel}");
                return _Step.Stop(RunResult.Terminated);
            }

            // Spawn a new goal with heterogeneous argument Terms
            // Per spec v2.16 section 1.1: Create CallEnv from argSlots
            var newEnv = new CallEnv(new Dictionary<int, Term>(cx.ArgSlots));

            // Create and enqueue new goal with unique ID
            var newGoalId = cx.Rt.NextGoalId++;
            var newGoalRef = new GoalRef(newGoalId, entryPc);

            // Format spawned goal as GLP predicate with arguments
            var fmtArgs = new List<string>();
            for (var i = 0; i < 10; i++)
            {
                var term = newEnv.Arg(i);
                if (term != null)
                {
                    // Use custom formatter if provided, otherwise fall back to static formatter
                    fmtArgs.Add(cx.TermFormatter != null
                        ? cx.TermFormatter(term, true)
                        : _formatTerm(cx.Rt, term));
                }
                else
                {
                    break;
                }
            }
            var goalStr = fmtArgs.Count == 0 ? op.ProcedureLabel : $"{op.ProcedureLabel}({string.Join(", ", fmtArgs)})";
            cx.SpawnedGoals.Add(goalStr);

            // Register environment with the runtime
            cx.Rt.SetGoalEnv(newGoalId, newEnv);

            // Inherit program from parent goal
            var parentProgram = cx.Rt.GetGoalProgram(cx.GoalId);
            if (parentProgram != null)
            {
                cx.Rt.SetGoalProgram(newGoalId, parentProgram);
            }

            // Enqueue the goal
            cx.Rt.Gq.Enqueue(newGoalRef);

            // Propagate infrastructure goal status to child goals
            if (cx.Rt.InfrastructureGoalIds.Contains(cx.GoalId))
            {
                cx.Rt.InfrastructureGoalIds.Add(newGoalId);
            }

            // Clear argument registers for next spawn
            cx.ArgSlots.Clear();
        }
        return _Step.Advance();
    }

    private _Step ExecSuspendEnd(RunnerContext cx, SuspendEnd op)
    {
        // Legacy SuspendEnd (use NoMoreClauses instead)
        if (cx.U.Count > 0)
        {
            // feature-020 equiv-trace: mirror ExecNoMoreClauses suspend emission.
            if (EquivTrace.Enabled)
            {
                EquivTrace.Unify("suspend", cx.U);
                foreach (var r in cx.U) EquivTrace.Suspend(r, cx.GoalId);
            }
            cx.Rt.SuspendGoalFCP(goalId: cx.GoalId, kappa: cx.Kappa, readerVarIds: cx.U);
            cx.U.Clear();
            cx.InBody = false;
            return _Step.Stop(RunResult.Suspended);
        }
        // U is empty - all clauses failed definitively (no suspension)
        EquivTrace.Unify("fail");  // feature-020 (no-op unless enabled)
        cx.InBody = false;
        return _Step.Stop(RunResult.Terminated);
    }

    private _Step ExecTailStep(RunnerContext cx, TailStep op)
    {
        var shouldYield = cx.Rt.TailReduce(cx.GoalId);
        if (shouldYield)
        {
            cx.Rt.Gq.Enqueue(new GoalRef(cx.GoalId, cx.Kappa));
            return _Step.Stop(RunResult.Yielded);
        }
        return _Step.Jump(_prog.Labels[op.Label]);
    }

    private _Step ExecTransmit(RunnerContext cx, Transmit op)
    {
        // Dynamic RPC to module resolved at runtime
        // Following FCP: transmit # {ModuleVar, Goal}
        // Resolves module name from variable, looks up in registry,
        // Routes via GLP channels to target module.
        if (cx.InBody)
        {
            // Collect arguments from argSlots
            var args = new List<Term>();
            for (var i = 0; i < (int)op.Arity; i++)
            {
                cx.ArgSlots.TryGetValue(i, out var arg);
                if (arg != null) args.Add(arg);
            }

            // Get module name from clause variable
            cx.ClauseVars.TryGetValue((int)op.ModuleVarIndex, out var moduleVar);

            // Resolve module name from variable
            string? moduleName = null;
            if (moduleVar is ConstTerm constModuleVar)
            {
                moduleName = constModuleVar.Value?.ToString();
            }
            else if (moduleVar is VarRef varRefModuleVar)
            {
                // Dereference variable to get bound value
                var deref = cx.Rt.Heap.Dereference(varRefModuleVar);
                if (deref is ConstTerm derefConst)
                {
                    moduleName = derefConst.Value?.ToString();
                }
            }

            if (moduleName != null)
            {
                // Check GLP channel first (Phase 5: RPC routing via GLP channels)
                if (cx.Rt.GlpChannels.TryGetValue(moduleName, out var glpChannel) && glpChannel != null)
                {
                    // Route via GLP channel — build goal term, send on channel
                    var goalTerm = new StructTerm(op.Functor, args);
                    var activations = glpChannel.Send(goalTerm);
                    foreach (var act in activations)
                    {
                        cx.Rt.EnqueueReactivatedGoal(act);
                    }
                    if (cx.DebugOutput)
                    {
                        Console.WriteLine($"[MODULE] Transmit (GLP channel): -> {moduleName} # {op.Functor}/{op.Arity}");
                    }
                }
                else
                {
                    Console.WriteLine($"ERROR: Transmit: module {moduleName} not activated (no GLP channel for {op.Functor}/{op.Arity})");
                    return _Step.Stop(RunResult.Terminated);
                }
            }
            else
            {
                Console.WriteLine($"ERROR: Transmit: could not resolve module name from X{op.ModuleVarIndex} ({op.Functor}/{op.Arity})");
                return _Step.Stop(RunResult.Terminated);
            }
            cx.ArgSlots.Clear();
        }
        return _Step.Advance();
    }

    private _Step ExecTryNextClause(RunnerContext cx, TryNextClause op)
    {
        // try_next_clause: Soft-fail to next clause (spec 2.4)
        // When HEAD/GUARD fails, discard σ̂w, union Si to U, jump to next ClauseTry
        var pc = cx.Pc;
        _softFailToNextClause(cx, pc);
        return _Step.Jump(_findNextClauseTry(pc));
    }

    private _Step ExecUnifyConstant(RunnerContext cx, UnifyConstant op)
    {
        var pc = cx.Pc;
        // Match constant at current S position
        if (cx.Mode == UnifyMode.Write)
        {
            // WRITE mode: Add constant to structure being built
            if (cx.CurrentStructure is _TentativeStruct struct1)
            {
                struct1.Args[cx.S] = op.Value;
                cx.S++; // Advance to next arg

                // Check if structure is complete
                if (cx.S >= struct1.Args.Count)
                {
                    // Structure complete - bind the target writer (stored at clauseVars[-1])
                    if (cx.ClauseVars.TryGetValue(-1, out var t1) && t1 is int targetWriterId)
                    {
                        // Convert args to Terms
                        var termArgs = new List<Term>();
                        foreach (var arg in struct1.Args)
                        {
                            if (arg is Term at) termArgs.Add(at);
                            else termArgs.Add(new ConstTerm(arg));
                        }
                        // Bind the writer to the completed structure
                        cx.Rt.Heap.BindWriterStruct(targetWriterId, struct1.Functor, termArgs);

                        // Reset structure building state
                        cx.CurrentStructure = null;
                        cx.Mode = UnifyMode.Read;
                        cx.S = 0;
                        cx.ClauseVars.Remove(-1);
                    }
                }
            }
            else if (cx.CurrentStructure is StructTerm struct2)
            {
                // Structure building (BODY or guard argument)
                var sargs = MutableArgs(struct2);
                // If value is already a Term (e.g., StructTerm), use it directly
                // Otherwise wrap in ConstTerm
                sargs[cx.S] = op.Value is Term ovt ? ovt : new ConstTerm(op.Value);
                cx.S++; // Advance to next arg

                // Check if structure is complete
                if (cx.S >= sargs.Count)
                {
                    // Check if we're in guard argument building mode (pre-commit)
                    if (cx.GuardArgSlot != null)
                    {
                        // Guard argument mode: store structure directly in argSlots
                        cx.ArgSlots[cx.GuardArgSlot.Value] = struct2;
                        cx.CurrentStructure = null;
                        cx.Mode = UnifyMode.Read;
                        cx.S = 0;
                        cx.GuardArgSlot = null;
                    }
                    else
                    {
                        // BODY phase: bind the target writer (stored at clauseVars[-1])
                        if (cx.ClauseVars.TryGetValue(-1, out var t2) && t2 is int targetWriterId)
                        {
                            // Bind the writer to the completed structure
                            cx.Rt.Heap.BindWriterStruct(targetWriterId, struct2.Functor, sargs);

                            // Put the structure reference into argSlots if we have a target slot
                            if (cx.ClauseVars.TryGetValue(-2, out var ts) && ts is int targetSlot && targetSlot >= 0 && targetSlot < 10)
                            {
                                cx.ArgSlots[targetSlot] = new VarRef(cx.Rt.Heap.PairedReaderAddr(targetWriterId));
                                cx.ClauseVars.Remove(-2);
                            }

                            // Reset structure building state
                            cx.CurrentStructure = null;
                            cx.Mode = UnifyMode.Read;
                            cx.S = 0;
                            cx.ClauseVars.Remove(-1);
                        }
                    }
                }
            }
        }
        else
        {
            // READ mode: Verify value at S position matches constant
            if (cx.CurrentStructure is StructTerm struct3)
            {
                if (cx.S < struct3.Args.Count)
                {
                    var value = struct3.Args[cx.S];

                    if (value is ConstTerm vc && NumEquals(vc.Value, op.Value))
                    {
                        // Constant matches - advance
                        cx.S++;
                    }
                    else if (value is VarRef vw && cx.Rt.Heap.IsWriter(vw.Addr))
                    {
                        // Writer variable - bind to constant in σ̂w
                        var wid = vw.Addr;
                        if (cx.Rt.Heap.IsWriterBound(wid))
                        {
                            // Already bound - check if it matches
                            var boundValue = cx.Rt.Heap.ValueOfWriter(wid);
                            if (boundValue is ConstTerm bc && NumEquals(bc.Value, op.Value))
                            {
                                cx.S++; // Match successful
                            }
                            else
                            {
                                // Bound to different value - fail
                                _softFailToNextClause(cx, pc);
                                return _Step.Jump(_findNextClauseTry(pc));
                            }
                        }
                        else
                        {
                            // Unbound writer - add tentative binding to σ̂w
                            cx.SigmaHat[wid] = new ConstTerm(op.Value);
                            cx.S++;
                        }
                    }
                    else if (value is VarRef vr && cx.Rt.Heap.IsReader(vr.Addr))
                    {
                        // Reader variable - check if bound, else suspend
                        var rid = vr.Addr;
                        if (cx.Rt.Heap.IsReaderBound(rid))
                        {
                            // Reader is bound - check if it matches
                            var boundValue = cx.Rt.Heap.GetReaderValue(rid);
                            if (boundValue is ConstTerm bc && NumEquals(bc.Value, op.Value))
                            {
                                cx.S++; // Match successful
                            }
                            else
                            {
                                // Bound to different value - fail
                                _softFailToNextClause(cx, pc);
                                return _Step.Jump(_findNextClauseTry(pc));
                            }
                        }
                        else
                        {
                            // Unbound reader - add to Si and continue (two-phase)
                            cx.Si.Add(rid);
                            cx.S++;
                        }
                    }
                    else
                    {
                        // Mismatch - soft fail
                        _softFailToNextClause(cx, pc);
                        return _Step.Jump(_findNextClauseTry(pc));
                    }
                }
                else
                {
                    // Structure arity mismatch - soft fail
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else
            {
                // Not a structure - skip (HeadStructure may have added to Si for unbound reader)
                return _Step.Advance();
            }
        }
        return _Step.Advance();
    }

    /// <summary>
    /// View a StructTerm's args as a mutable list. The interpreter builds StructTerm
    /// instances with a backing List&lt;Term&gt; (PutStructure / guard-arg building), so the
    /// WRITE-mode arms mutate args[S] in place — matching the Dart `struct.args[cx.S] = …`.
    /// </summary>
    private static List<Term> MutableArgs(StructTerm st) => (List<Term>)st.Args;

    private _Step ExecUnifyStructure(RunnerContext cx, UnifyStructure op)
    {
        var pc = cx.Pc;
        // UnifyStructure: Process nested structure at S position
        if (cx.Mode == UnifyMode.Read)
        {
            // READ mode: Match structure at args[S]
            if (cx.CurrentStructure is StructTerm parent)
            {
                if (cx.S < parent.Args.Count)
                {
                    object? value = parent.Args[cx.S];

                    // CRITICAL FIX: Dereference if it's a variable reference.
                    if (value is VarRef vrf)
                    {
                        var addr = vrf.Addr;
                        // Check sigma-hat first (tentative bindings)
                        if (cx.SigmaHat.TryGetValue(addr, out var shVal))
                        {
                            value = shVal;
                        }
                        // Then check heap bindings
                        else if (cx.Rt.Heap.IsBound(addr))
                        {
                            value = cx.Rt.Heap.GetValue(addr);
                        }
                    }

                    if (value is StructTerm vst && vst.Functor == op.Functor && vst.Args.Count == op.Arity)
                    {
                        // Match! Enter this structure
                        cx.CurrentStructure = value;
                        cx.S = 0;
                    }
                    else if (value is VarRef vw && cx.Rt.Heap.IsWriter(vw.Addr))
                    {
                        // Mode conversion: unbound writer where structure expected.
                        var nested = new _TentativeStruct(op.Functor, (int)op.Arity, NullArgs((int)op.Arity));

                        // Record binding in σ̂w (writer will be bound to this structure at commit)
                        cx.SigmaHat[vw.Addr] = nested;

                        // Switch to WRITE mode
                        cx.Mode = UnifyMode.Write;

                        // Enter the nested structure
                        cx.CurrentStructure = nested;
                        cx.S = 0;
                    }
                    else if (value is VarRef vr && cx.Rt.Heap.IsReader(vr.Addr))
                    {
                        // Unbound reader where structure expected - suspend on unbound reader
                        cx.U.Add(vr.Addr);
                        _softFailToNextClause(cx, pc);
                        return _Step.Jump(_findNextClauseTry(pc));
                    }
                    else
                    {
                        // Mismatch - fail to next clause
                        _softFailToNextClause(cx, pc);
                        return _Step.Jump(_findNextClauseTry(pc));
                    }
                }
            }
        }
        else
        {
            // WRITE mode: Create nested structure at args[S]
            if (cx.CurrentStructure is _TentativeStruct parent)
            {
                var nested = new _TentativeStruct(op.Functor, (int)op.Arity, NullArgs((int)op.Arity));
                parent.Args[cx.S] = nested;
                cx.CurrentStructure = nested;
                cx.S = 0;
            }
        }
        return _Step.Advance();
    }

    private _Step ExecUnifyVoid(RunnerContext cx, UnifyVoid op)
    {
        // Skip/create void (anonymous) variables
        if (cx.Mode == UnifyMode.Write)
        {
            // WRITE mode: Create fresh unbound variables
            if (cx.CurrentStructure is _TentativeStruct struct1)
            {
                for (var i = 0; i < op.Count && cx.S < struct1.Args.Count; i++)
                {
                    struct1.Args[cx.S] = null; // Void/unbound
                    cx.S++;
                }
            }
        }
        else
        {
            // READ mode: Skip over positions
            cx.S += (int)op.Count;
        }
        return _Step.Advance();
    }

    private _Step ExecUnionSiAndGoto(RunnerContext cx, UnionSiAndGoto op)
    {
        // Legacy instruction (deprecated, use ClauseNext instead).
        // Si removed - U updated directly by HEAD/GUARD opcodes.
        cx.ClearClause();
        return _Step.Jump(_prog.Labels[op.Label]);
    }

    // ── v2 arms ──

    private _Step ExecV2GetValue(RunnerContext cx, V2.GetValue op)
    {
        var pc = cx.Pc;
        var varIndex = (int)op.VarIndex;
        var argSlot = (int)op.ArgSlot;
        var isReaderMode = op.IsReader;

        var arg = _getArg(cx, argSlot);
        if (arg == null)
        {
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        cx.ClauseVars.TryGetValue(varIndex, out var storedValue);
        if (storedValue == null)
        {
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        if (!isReaderMode)
        {
            // GetWriterValue logic: Unify argument with clause WRITER variable
            if (arg is VarRef aw && cx.Rt.Heap.IsWriter(aw.Addr))
            {
                var argBound = cx.Rt.Heap.IsWriterBound(aw.Addr);
                if (argBound)
                {
                    var argValue = cx.Rt.Heap.ValueOfWriter(aw.Addr);
                    if (storedValue is int swi)
                    {
                        var storedBound = cx.Rt.Heap.IsWriterBound(swi);
                        if (storedBound)
                        {
                            var storedVal = cx.Rt.Heap.ValueOfWriter(swi);
                            bool match;
                            if (argValue is ConstTerm avc && storedVal is ConstTerm svc)
                                match = NumEquals(avc.Value, svc.Value);
                            else if (argValue is StructTerm avs && storedVal is StructTerm svs)
                                match = avs.Functor == svs.Functor && avs.Args.Count == svs.Args.Count;
                            else
                                match = Equals(argValue, storedVal);
                            if (!match)
                            {
                                _softFailToNextClause(cx, pc);
                                return _Step.Jump(_findNextClauseTry(pc));
                            }
                        }
                        else
                        {
                            cx.SigmaHat[swi] = argValue;
                        }
                    }
                    else if (storedValue is Term svtm)
                    {
                        bool match;
                        if (argValue is ConstTerm avc && svtm is ConstTerm svc)
                            match = NumEquals(avc.Value, svc.Value);
                        else if (argValue is StructTerm avs && svtm is StructTerm svs)
                            match = avs.Functor == svs.Functor && avs.Args.Count == svs.Args.Count;
                        else
                            match = Equals(argValue, svtm);
                        if (!match)
                        {
                            _softFailToNextClause(cx, pc);
                            return _Step.Jump(_findNextClauseTry(pc));
                        }
                    }
                }
                else
                {
                    if (storedValue is int swi)
                    {
                        cx.SigmaHat.TryGetValue(swi, out var freshVarBinding);
                        if (freshVarBinding != null)
                        {
                            cx.SigmaHat[aw.Addr] = freshVarBinding;
                        }
                        else if (aw.Addr != swi)
                        {
                            _softFailToNextClause(cx, pc);
                            return _Step.Jump(_findNextClauseTry(pc));
                        }
                    }
                    else if (storedValue is Term svtm)
                    {
                        cx.SigmaHat[aw.Addr] = svtm;
                    }
                }
            }
            else if (arg is VarRef ar && cx.Rt.Heap.IsReader(ar.Addr))
            {
                var rid = ar.Addr;
                if (cx.Rt.Heap.IsReaderBound(rid))
                {
                    var readerValue = cx.Rt.Heap.GetReaderValue(rid);
                    if (storedValue is int swi)
                    {
                        cx.SigmaHat[swi] = readerValue;
                    }
                    else if (!Equals(storedValue, readerValue))
                    {
                        _softFailToNextClause(cx, pc);
                        return _Step.Jump(_findNextClauseTry(pc));
                    }
                }
                else
                {
                    // Reader is unbound - alias storedValue to reader
                    var wid = cx.Rt.Heap.TryWriterForReader(rid);
                    if (storedValue is int swi)
                    {
                        if (wid != null)
                        {
                            cx.SigmaHat[swi] = new VarRef(cx.Rt.Heap.PairedReaderAddr(wid.Value));
                        }
                        else
                        {
                            // Imported reader - alias to reader directly
                            cx.SigmaHat[swi] = new VarRef(rid);
                        }
                    }
                }
            }
            else if (arg is ConstTerm ac)
            {
                if (storedValue is int swi)
                {
                    cx.SigmaHat[swi] = arg;
                }
                else if (storedValue is ConstTerm svc && !NumEquals(svc.Value, ac.Value))
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else if (arg is StructTerm asct)
            {
                if (storedValue is int swi)
                {
                    cx.SigmaHat[swi] = arg;
                }
                else if (storedValue is StructTerm svs && svs.Functor != asct.Functor)
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
        }
        else
        {
            // GetReaderValue logic: Unify argument with clause READER variable
            if (arg is VarRef aw && cx.Rt.Heap.IsWriter(aw.Addr))
            {
                // Goal has writer, head has reader - bind goal writer to stored value
                if (storedValue is VarRef svr)
                {
                    cx.SigmaHat[aw.Addr] = svr;
                }
                else if (storedValue is int swi)
                {
                    if (cx.Rt.Heap.IsReaderBound(swi))
                    {
                        var readerValue = cx.Rt.Heap.GetReaderValue(swi);
                        cx.SigmaHat[aw.Addr] = readerValue;
                    }
                    else
                    {
                        return _Step.Jump(_suspendAndFail(cx, swi, pc));
                    }
                }
                else if (storedValue is Term svtm)
                {
                    cx.SigmaHat[aw.Addr] = svtm;
                }
            }
            else if (arg is VarRef ar && cx.Rt.Heap.IsReader(ar.Addr))
            {
                var wid = cx.Rt.Heap.TryWriterForReader(ar.Addr);
                // For imported readers (wid == null), compare reader addresses directly
                var compareTo = wid ?? ar.Addr;
                if (storedValue is int swi && compareTo != swi)
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else if (arg is ConstTerm || arg is StructTerm)
            {
                if (!Equals(storedValue, arg))
                {
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
        }
        return _Step.Advance();
    }

    private _Step ExecV2GetVariable(RunnerContext cx, V2.GetVariable op)
    {
        var pc = cx.Pc;
        var varIndex = (int)op.VarIndex;
        var argSlot = (int)op.ArgSlot;
        var isReaderMode = op.IsReader;

        var arg = _getArg(cx, argSlot);
        if (arg == null)
        {
            _softFailToNextClause(cx, pc);
            return _Step.Jump(_findNextClauseTry(pc));
        }

        if (!isReaderMode)
        {
            // GetWriterVariable logic: Load argument into clause WRITER variable
            cx.ClauseVars.TryGetValue(varIndex, out var existing);

            if (arg is VarRef aw && cx.Rt.Heap.IsWriter(aw.Addr))
            {
                if (existing is VarRef ew && cx.Rt.Heap.IsWriter(ew.Addr))
                {
                    // Both are writers - bind arg writer to existing writer's reader
                    cx.SigmaHat[aw.Addr] = new VarRef(cx.Rt.Heap.PairedReaderAddr(ew.Addr));
                }
                else if (existing is int ei)
                {
                    // existing is bare writer addr - bind arg to reader of it
                    cx.SigmaHat[aw.Addr] = new VarRef(cx.Rt.Heap.PairedReaderAddr(ei));
                }
                else
                {
                    // First occurrence: goal writer vs head writer
                    if (cx.Rt.Heap.IsWriterBound(aw.Addr))
                    {
                        // Goal writer already bound - use its value
                        var boundValue = cx.Rt.Heap.ValueOfWriter(aw.Addr);
                        cx.ClauseVars[varIndex] = boundValue;
                    }
                    else
                    {
                        // Goal writer unbound - store writer ref, clause can bind it later
                        cx.ClauseVars[varIndex] = arg;
                    }
                }
            }
            else if (arg is VarRef ar && cx.Rt.Heap.IsReader(ar.Addr))
            {
                if (cx.Rt.Heap.IsReaderBound(ar.Addr))
                {
                    var value = cx.Rt.Heap.GetReaderValue(ar.Addr);
                    if (existing is VarRef ew && cx.Rt.Heap.IsWriter(ew.Addr))
                    {
                        cx.SigmaHat[ew.Addr] = value;
                    }
                    else if (existing is int ei)
                    {
                        cx.SigmaHat[ei] = value;
                    }
                    else
                    {
                        cx.ClauseVars[varIndex] = value;
                    }
                }
                else
                {
                    // Reader is unbound - but clause expects a writer (isReaderMode=false)
                    if (existing is VarRef ew && cx.Rt.Heap.IsWriter(ew.Addr))
                    {
                        cx.SigmaHat[ew.Addr] = arg;  // arg is the reader VarRef
                    }
                    else if (existing is int ei)
                    {
                        cx.SigmaHat[ei] = arg;
                    }
                    else
                    {
                        // First occurrence - store the reader reference
                        cx.ClauseVars[varIndex] = arg;
                    }
                }
            }
            else if (arg is ConstTerm)
            {
                if (existing is VarRef ew && cx.Rt.Heap.IsWriter(ew.Addr)) cx.SigmaHat[ew.Addr] = arg;
                else if (existing is int ei) cx.SigmaHat[ei] = arg;
                else cx.ClauseVars[varIndex] = arg;
            }
            else if (arg is StructTerm)
            {
                if (existing is VarRef ew && cx.Rt.Heap.IsWriter(ew.Addr)) cx.SigmaHat[ew.Addr] = arg;
                else if (existing is int ei) cx.SigmaHat[ei] = arg;
                else cx.ClauseVars[varIndex] = arg;
            }
            else if (arg is Term)
            {
                // Handle other Term types (e.g., MutualRefTerm)
                if (existing is VarRef ew && cx.Rt.Heap.IsWriter(ew.Addr)) cx.SigmaHat[ew.Addr] = arg;
                else if (existing is int ei) cx.SigmaHat[ei] = arg;
                else cx.ClauseVars[varIndex] = arg;
            }
        }
        else
        {
            // GetReaderVariable logic: Load argument into clause READER variable
            cx.ClauseVars.TryGetValue(varIndex, out var existing);

            if (arg is VarRef aw && cx.Rt.Heap.IsWriter(aw.Addr))
            {
                // Goal writer → head reader (clause observes goal's variable)
                if (existing != null)
                {
                    if (existing is VarRef ew && cx.Rt.Heap.IsWriter(ew.Addr))
                    {
                        // existing is a writer - bind to its reader
                        cx.SigmaHat[aw.Addr] = new VarRef(cx.Rt.Heap.PairedReaderAddr(ew.Addr));
                    }
                    else if (existing is int ei)
                    {
                        // existing is bare writer addr - bind to reader of it
                        cx.SigmaHat[aw.Addr] = new VarRef(cx.Rt.Heap.PairedReaderAddr(ei));
                    }
                    else
                    {
                        // existing is already a reader or a term - use as-is
                        cx.SigmaHat[aw.Addr] = existing;
                    }
                }
                else
                {
                    // First occurrence: head reader observes goal writer
                    cx.ClauseVars[varIndex] = aw.Addr;
                }
            }
            else if (arg is VarRef ar && cx.Rt.Heap.IsReader(ar.Addr))
            {
                // Spec §12.2 Case 2: Reader × Reader = FAIL
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
            else if (arg is ConstTerm)
            {
                if (existing == null) cx.ClauseVars[varIndex] = arg;
            }
            else if (arg is StructTerm)
            {
                if (existing == null) cx.ClauseVars[varIndex] = arg;
            }
            else if (arg is Term)
            {
                // Handle other Term types (e.g., MutualRefTerm)
                if (existing == null) cx.ClauseVars[varIndex] = arg;
            }
        }
        return _Step.Advance();
    }

    private _Step ExecV2HeadVariable(RunnerContext cx, V2.HeadVariable op)
    {
        var pc = cx.Pc;
        if (cx.Mode == UnifyMode.Write)
        {
            // WRITE mode: Building a structure
            if (cx.CurrentStructure is _TentativeStruct struc)
            {
                // Check if this clause variable already has a value
                cx.ClauseVars.TryGetValue((int)op.VarIndex, out var existingValue);
                if (existingValue != null)
                {
                    // Variable already bound
                    if (op.IsReader && existingValue is int evw)
                    {
                        // Reader mode with variable address - use paired reader addr
                        struc.Args[cx.S] = new VarRef(cx.Rt.Heap.PairedReaderAddr(evw));
                    }
                    else
                    {
                        // Use value as is
                        struc.Args[cx.S] = existingValue;
                    }
                }
                else
                {
                    // New variable - create placeholder
                    var placeholder = new _ClauseVar((int)op.VarIndex, isWriter: !op.IsReader);
                    struc.Args[cx.S] = placeholder;
                    cx.ClauseVars[(int)op.VarIndex] = placeholder;
                }
                cx.S++; // Advance to next arg
            }
        }
        else
        {
            // READ mode: Extract value from structure at S position
            if (cx.CurrentStructure is StructTerm struc)
            {
                if (cx.S < struc.Args.Count)
                {
                    var value = struc.Args[cx.S];

                    // Check if variable already bound
                    cx.ClauseVars.TryGetValue((int)op.VarIndex, out var existingValue);
                    if (existingValue != null)
                    {
                        // Need to unify
                        if (!Equals(existingValue, value))
                        {
                            _softFailToNextClause(cx, pc);
                            return _Step.Jump(_findNextClauseTry(pc));
                        }
                    }
                    else
                    {
                        // First occurrence - store it
                        cx.ClauseVars[(int)op.VarIndex] = value;
                    }
                    cx.S++; // Advance to next arg
                }
                else
                {
                    // Structure arity mismatch - soft fail
                    _softFailToNextClause(cx, pc);
                    return _Step.Jump(_findNextClauseTry(pc));
                }
            }
            else
            {
                // Not a structure - soft fail
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
        }
        return _Step.Advance();
    }

    private _Step ExecV2PutVariable(RunnerContext cx, V2.PutVariable op)
    {
        var varIndex = (int)op.VarIndex;
        var argSlot = (int)op.ArgSlot;
        var isReaderMode = op.IsReader;

        cx.ClauseVars.TryGetValue(varIndex, out var value);

        if (value is VarRef vr)
        {
            // Already a VarRef - determine writer addr and store with appropriate mode
            var addr = vr.Addr;
            var isWriter = cx.Rt.Heap.IsWriter(addr);
            var isReader = cx.Rt.Heap.IsReader(addr);

            if (!isWriter && !isReader)
            {
                // Bound to ground value (ValueTag) - store on heap and pass VarRef
                var groundValue = cx.Rt.Heap.GetValue(addr);
                if (groundValue != null)
                {
                    var heapAddr = cx.Rt.Heap.StoreTermOnHeap(groundValue);
                    cx.ArgSlots[argSlot] = new VarRef(heapAddr);
                }
                else
                {
                    cx.ArgSlots[argSlot] = vr;  // Fallback: already VarRef
                }
            }
            else
            {
                // Writer or reader
                if (isWriter)
                {
                    var writerAddr = addr;
                    cx.ArgSlots[argSlot] = new VarRef(isReaderMode ? writerAddr + 1 : writerAddr);
                }
                else
                {
                    // Reader - try to get writer (will be null for imported readers)
                    var writerAddr = cx.Rt.Heap.TryWriterForReader(addr);
                    if (writerAddr != null)
                    {
                        cx.ArgSlots[argSlot] = new VarRef(isReaderMode ? writerAddr.Value + 1 : writerAddr.Value);
                    }
                    else
                    {
                        // Imported reader - no local writer; pass reader address directly
                        cx.ArgSlots[argSlot] = new VarRef(addr);
                    }
                }
            }
        }
        else if (value is int vi)
        {
            // Legacy: bare int ID (assumed to be writer addr)
            cx.ArgSlots[argSlot] = new VarRef(isReaderMode ? vi + 1 : vi);
        }
        else if (value is _ClauseVar && !isReaderMode)
        {
            // Placeholder (PutWriter only) - allocate fresh variable
            var (writerAddr, _) = cx.Rt.Heap.AllocateVariable();
            cx.ArgSlots[argSlot] = new VarRef(writerAddr);
            cx.ClauseVars[varIndex] = new VarRef(writerAddr);
        }
        else if (value is StructTerm vst && isReaderMode)
        {
            // Structure (PutReader only) - create fresh variable and bind it
            var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
            cx.Rt.Heap.BindWriterStruct(writerAddr, vst.Functor, new List<Term>(vst.Args));
            cx.ArgSlots[argSlot] = new VarRef(readerAddr);
        }
        else if (value is ConstTerm vct && isReaderMode)
        {
            // Constant (PutReader only) - create fresh variable and bind it
            var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
            cx.Rt.Heap.BindWriterConst(writerAddr, vct.Value);
            cx.ArgSlots[argSlot] = new VarRef(readerAddr);
        }
        else if (value == null)
        {
            // First occurrence - allocate fresh variable
            var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
            cx.ClauseVars[varIndex] = new VarRef(writerAddr);
            cx.ArgSlots[argSlot] = new VarRef(isReaderMode ? readerAddr : writerAddr);
        }
        else if (value is Term vtm && isReaderMode)
        {
            // Ground term (e.g., MutualRefTerm) - store on heap and pass VarRef
            var heapAddr = cx.Rt.Heap.StoreTermOnHeap(vtm);
            cx.ArgSlots[argSlot] = new VarRef(heapAddr);
        }
        else
        {
            // Unexpected value (Dart logged a WARNING and fell through). No-op.
        }
        return _Step.Advance();
    }

    private _Step ExecV2SetVariable(RunnerContext cx, V2.SetVariable op)
    {
        var varIndex = (int)op.VarIndex;
        var isReaderMode = op.IsReader;

        if (cx.InBody && cx.Mode == UnifyMode.Write && cx.CurrentStructure is StructTerm struc)
        {
            // Check what value exists in clause variables
            cx.ClauseVars.TryGetValue(varIndex, out var existingValue);
            var sargs = MutableArgs(struc);

            if (existingValue is VarRef evr)
            {
                var addr = evr.Addr;
                if (isReaderMode && cx.Rt.Heap.IsWriter(addr))
                {
                    sargs[cx.S] = new VarRef(cx.Rt.Heap.PairedReaderAddr(addr));
                }
                else if (!isReaderMode && cx.Rt.Heap.IsReader(addr))
                {
                    sargs[cx.S] = new VarRef(cx.Rt.Heap.TryWriterForReader(addr)!.Value);
                }
                else
                {
                    sargs[cx.S] = new VarRef(addr);
                }
            }
            else if (existingValue is int evi)
            {
                // Legacy: bare writer addr
                if (isReaderMode)
                    sargs[cx.S] = new VarRef(cx.Rt.Heap.PairedReaderAddr(evi));
                else
                    sargs[cx.S] = new VarRef(evi);
            }
            else if (existingValue is Term evt)
            {
                // Term (ConstTerm, StructTerm, etc.): embed directly in structure
                sargs[cx.S] = evt;
            }
            else
            {
                // Uninitialized: allocate new variable
                var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
                cx.ClauseVars[varIndex] = new VarRef(writerAddr);
                sargs[cx.S] = new VarRef(isReaderMode ? readerAddr : writerAddr);
            }
            cx.S++;

            // Check if structure is complete
            if (cx.S >= sargs.Count)
            {
                cx.ClauseVars.TryGetValue(-1, out var targetValue);
                int? targetWriterAddr = targetValue is VarRef tvr ? tvr.Addr : (targetValue is int tvi ? tvi : (int?)null);

                if (targetWriterAddr != null)
                {
                    var acts = cx.Rt.Heap.BindWriterStruct(targetWriterAddr.Value, struc.Functor, sargs);
                    foreach (var a in acts)
                    {
                        cx.Rt.Gq.Enqueue(a);
                        cx.OnActivation?.Invoke(a);
                    }

                    // SetWriter-specific: Store VarRef in argSlots ONLY if no parent
                    if (!isReaderMode && cx.ParentStack.Count == 0)
                    {
                        if (cx.ClauseVars.TryGetValue(-2, out var ts) && ts is int targetSlot && targetSlot >= 0 && targetSlot < 10)
                        {
                            cx.ArgSlots[targetSlot] = new VarRef(cx.Rt.Heap.PairedReaderAddr(targetWriterAddr.Value));
                            cx.ClauseVars.Remove(-2);
                        }
                    }
                }

                // Handle parent structure restoration - pop from stack
                if (cx.ParentStack.Count > 0 && targetWriterAddr != null)
                {
                    var nestedWriterAddr = targetWriterAddr.Value;
                    var parent = cx.ParentStack[cx.ParentStack.Count - 1];
                    cx.ParentStack.RemoveAt(cx.ParentStack.Count - 1);
                    var parentWriterId = parent.WriterId;

                    if (parent.Structure is StructTerm parentStruct)
                    {
                        MutableArgs(parentStruct)[parent.S] = new VarRef(cx.Rt.Heap.PairedReaderAddr(nestedWriterAddr));
                    }

                    cx.CurrentStructure = parent.Structure;
                    cx.S = parent.S + 1;
                    cx.Mode = parent.Mode;
                    cx.ClauseVars[-1] = parentWriterId;

                    // Check if parent is now complete - and recursively complete ancestors
                    while (cx.CurrentStructure is StructTerm parentStruct2)
                    {
                        cx.ClauseVars.TryGetValue(-1, out var currentWriterAddr);
                        int? currentWriterAddrInt = currentWriterAddr is VarRef cwr ? cwr.Addr : (currentWriterAddr is int cwi ? cwi : (int?)null);
                        var pargs = MutableArgs(parentStruct2);

                        if (cx.S >= pargs.Count && currentWriterAddrInt != null)
                        {
                            var acts = cx.Rt.Heap.BindWriterStruct(currentWriterAddrInt.Value, parentStruct2.Functor, pargs);
                            foreach (var a in acts)
                            {
                                cx.Rt.Gq.Enqueue(a);
                                cx.OnActivation?.Invoke(a);
                            }

                            if (cx.ParentStack.Count > 0)
                            {
                                var ancestor = cx.ParentStack[cx.ParentStack.Count - 1];
                                cx.ParentStack.RemoveAt(cx.ParentStack.Count - 1);
                                if (ancestor.Structure is StructTerm ancestorStruct)
                                {
                                    // Use reader address (writer + 1) for structure args
                                    MutableArgs(ancestorStruct)[ancestor.S] = new VarRef(currentWriterAddrInt.Value + 1);
                                }
                                cx.CurrentStructure = ancestor.Structure;
                                cx.S = ancestor.S + 1;
                                cx.Mode = ancestor.Mode;
                                cx.ClauseVars[-1] = ancestor.WriterId;
                            }
                            else
                            {
                                // No more ancestors - store in argSlots and reset
                                if (cx.ClauseVars.TryGetValue(-2, out var pts) && pts is int parentTargetSlot && parentTargetSlot >= 0 && parentTargetSlot < 10)
                                {
                                    // Use reader address (writer + 1) for argSlots
                                    cx.ArgSlots[parentTargetSlot] = new VarRef(currentWriterAddrInt.Value + 1);
                                    cx.ClauseVars.Remove(-2);
                                }
                                cx.CurrentStructure = null;
                                cx.Mode = UnifyMode.Read;
                                cx.S = 0;
                                cx.ClauseVars.Remove(-1);
                                break;
                            }
                        }
                        else
                        {
                            // Parent not complete yet, stop
                            break;
                        }
                    }
                }
                else
                {
                    cx.CurrentStructure = null;
                    cx.Mode = UnifyMode.Read;
                    cx.S = 0;
                    cx.ClauseVars.Remove(-1);
                }
            }
        }
        return _Step.Advance();
    }

    private _Step ExecV2UnifyVariable(RunnerContext cx, V2.UnifyVariable op)
    {
        var pc = cx.Pc;
        var varIndex = (int)op.VarIndex;
        var isReaderMode = op.IsReader;

        if (cx.Mode == UnifyMode.Write)
        {
            // WRITE mode: Add variable to structure being built
            if (cx.CurrentStructure is _TentativeStruct struc)
            {
                // HEAD phase tentative structure
                cx.ClauseVars.TryGetValue(varIndex, out var clauseVarValue);

                if (clauseVarValue is VarRef cvr)
                {
                    var addr = cvr.Addr;
                    if (cx.Rt.Heap.IsValue(addr))
                    {
                        var groundValue = cx.Rt.Heap.GetValue(addr);
                        if (groundValue != null)
                        {
                            if (isReaderMode)
                            {
                                var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
                                cx.SigmaHat[writerAddr] = groundValue;
                                struc.Args[cx.S] = new VarRef(readerAddr);
                            }
                            else
                            {
                                struc.Args[cx.S] = groundValue;
                            }
                        }
                        else
                        {
                            struc.Args[cx.S] = clauseVarValue;
                        }
                    }
                    else if (isReaderMode && cx.Rt.Heap.IsWriter(addr))
                    {
                        struc.Args[cx.S] = new VarRef(cx.Rt.Heap.PairedReaderAddr(addr));
                    }
                    else if (!isReaderMode && cx.Rt.Heap.IsReader(addr))
                    {
                        struc.Args[cx.S] = new VarRef(cx.Rt.Heap.TryWriterForReader(addr)!.Value);
                    }
                    else
                    {
                        struc.Args[cx.S] = new VarRef(addr);
                    }
                }
                else if (clauseVarValue is int cvi)
                {
                    if (isReaderMode)
                        struc.Args[cx.S] = new VarRef(cx.Rt.Heap.PairedReaderAddr(cvi));
                    else
                        struc.Args[cx.S] = new VarRef(cvi);
                }
                else if (clauseVarValue is Term cvt)
                {
                    if (isReaderMode)
                    {
                        var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
                        cx.SigmaHat[writerAddr] = cvt;
                        struc.Args[cx.S] = new VarRef(readerAddr);
                    }
                    else
                    {
                        struc.Args[cx.S] = cvt;
                    }
                }
                else if (clauseVarValue is _TentativeStruct)
                {
                    struc.Args[cx.S] = clauseVarValue;
                }
                else if (clauseVarValue == null)
                {
                    var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
                    cx.ClauseVars[varIndex] = new VarRef(writerAddr);
                    struc.Args[cx.S] = new VarRef(isReaderMode ? readerAddr : writerAddr);
                }
                else
                {
                    struc.Args[cx.S] = new _ClauseVar(varIndex, isWriter: !isReaderMode);
                }
                cx.S++;
            }
            else if (cx.CurrentStructure is StructTerm structTerm)
            {
                // BODY phase structure building
                var sargs = MutableArgs(structTerm);
                cx.ClauseVars.TryGetValue(varIndex, out var clauseVarValue);

                if (clauseVarValue is VarRef cvr)
                {
                    var addr = cvr.Addr;
                    if (cx.Rt.Heap.IsValue(addr))
                    {
                        var groundValue = cx.Rt.Heap.GetValue(addr);
                        if (groundValue != null)
                        {
                            if (isReaderMode)
                            {
                                var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
                                cx.Rt.Heap.BindVariable(writerAddr, groundValue);
                                sargs[cx.S] = new VarRef(readerAddr);
                            }
                            else
                            {
                                sargs[cx.S] = groundValue;
                            }
                        }
                        else
                        {
                            sargs[cx.S] = cvr;
                        }
                    }
                    else if (isReaderMode && cx.Rt.Heap.IsWriter(addr))
                    {
                        sargs[cx.S] = new VarRef(cx.Rt.Heap.PairedReaderAddr(addr));
                    }
                    else if (!isReaderMode && cx.Rt.Heap.IsReader(addr))
                    {
                        sargs[cx.S] = new VarRef(cx.Rt.Heap.TryWriterForReader(addr)!.Value);
                    }
                    else
                    {
                        sargs[cx.S] = new VarRef(addr);
                    }
                }
                else if (clauseVarValue is int cvi)
                {
                    if (isReaderMode)
                        sargs[cx.S] = new VarRef(cx.Rt.Heap.PairedReaderAddr(cvi));
                    else
                        sargs[cx.S] = new VarRef(cvi);
                }
                else if (clauseVarValue is Term cvt)
                {
                    if (isReaderMode)
                    {
                        var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
                        cx.Rt.Heap.BindVariable(writerAddr, cvt);
                        sargs[cx.S] = new VarRef(readerAddr);
                    }
                    else
                    {
                        sargs[cx.S] = cvt;
                    }
                }
                else if (clauseVarValue == null)
                {
                    var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();
                    cx.ClauseVars[varIndex] = new VarRef(writerAddr);
                    sargs[cx.S] = new VarRef(isReaderMode ? readerAddr : writerAddr);
                }
                cx.S++;

                // Check if structure is complete
                if (cx.S >= sargs.Count)
                {
                    if (cx.GuardArgSlot != null)
                    {
                        // Guard argument mode: store structure directly in argSlots
                        cx.ArgSlots[cx.GuardArgSlot.Value] = structTerm;
                        cx.CurrentStructure = null;
                        cx.Mode = UnifyMode.Read;
                        cx.S = 0;
                        cx.GuardArgSlot = null;
                    }
                    else
                    {
                        // BODY phase: bind to heap writer
                        cx.ClauseVars.TryGetValue(-1, out var targetValue);
                        int? targetWriterAddr = targetValue is VarRef tvr ? tvr.Addr : (targetValue is int tvi ? tvi : (int?)null);

                        if (targetWriterAddr != null)
                        {
                            var acts = cx.Rt.Heap.BindWriterStruct(targetWriterAddr.Value, structTerm.Functor, sargs);
                            foreach (var a in acts)
                            {
                                cx.Rt.Gq.Enqueue(a);
                                cx.OnActivation?.Invoke(a);
                            }
                        }

                        // Handle parent structure restoration - pop from stack
                        if (cx.ParentStack.Count > 0 && targetWriterAddr != null)
                        {
                            var nestedWriterAddr = targetWriterAddr.Value;
                            var parent = cx.ParentStack[cx.ParentStack.Count - 1];
                            cx.ParentStack.RemoveAt(cx.ParentStack.Count - 1);
                            var parentWriterId = parent.WriterId;

                            if (parent.Structure is StructTerm parentStruct)
                            {
                                MutableArgs(parentStruct)[parent.S] = new VarRef(cx.Rt.Heap.PairedReaderAddr(nestedWriterAddr));
                            }

                            cx.CurrentStructure = parent.Structure;
                            cx.S = parent.S + 1;
                            cx.Mode = parent.Mode;
                            cx.ClauseVars[-1] = parentWriterId;

                            // Check if parent is now complete - and recursively complete ancestors
                            while (cx.CurrentStructure is StructTerm parentStruct2)
                            {
                                cx.ClauseVars.TryGetValue(-1, out var currentWriterId);
                                int? currentWriterAddrInt = currentWriterId is VarRef cvr2 ? cvr2.Addr : (currentWriterId is int cwi ? cwi : (int?)null);
                                var pargs = MutableArgs(parentStruct2);

                                if (cx.S >= pargs.Count && currentWriterAddrInt != null)
                                {
                                    var acts = cx.Rt.Heap.BindWriterStruct(currentWriterAddrInt.Value, parentStruct2.Functor, pargs);
                                    foreach (var a in acts)
                                    {
                                        cx.Rt.Gq.Enqueue(a);
                                        cx.OnActivation?.Invoke(a);
                                    }

                                    if (cx.ParentStack.Count > 0)
                                    {
                                        var ancestor = cx.ParentStack[cx.ParentStack.Count - 1];
                                        cx.ParentStack.RemoveAt(cx.ParentStack.Count - 1);
                                        if (ancestor.Structure is StructTerm ancestorStruct)
                                        {
                                            MutableArgs(ancestorStruct)[ancestor.S] = new VarRef(cx.Rt.Heap.PairedReaderAddr(currentWriterAddrInt.Value));
                                        }
                                        cx.CurrentStructure = ancestor.Structure;
                                        cx.S = ancestor.S + 1;
                                        cx.Mode = ancestor.Mode;
                                        cx.ClauseVars[-1] = ancestor.WriterId;
                                    }
                                    else
                                    {
                                        // No more ancestors - store in argSlots and reset
                                        if (cx.ClauseVars.TryGetValue(-2, out var pts) && pts is int parentTargetSlot && parentTargetSlot >= 0 && parentTargetSlot < 10)
                                        {
                                            cx.ArgSlots[parentTargetSlot] = new VarRef(cx.Rt.Heap.PairedReaderAddr(currentWriterAddrInt.Value));
                                            cx.ClauseVars.Remove(-2);
                                        }
                                        cx.CurrentStructure = null;
                                        cx.Mode = UnifyMode.Read;
                                        cx.S = 0;
                                        cx.ClauseVars.Remove(-1);
                                        break;
                                    }
                                }
                                else
                                {
                                    // Parent not complete yet, stop
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // No parent - store in argSlots and reset
                            if (cx.ClauseVars.TryGetValue(-2, out var ts) && ts is int targetSlot && targetSlot >= 0 && targetSlot < 10)
                            {
                                cx.ArgSlots[targetSlot] = new VarRef(cx.Rt.Heap.PairedReaderAddr(targetWriterAddr!.Value));
                                cx.ClauseVars.Remove(-2);
                            }
                            cx.CurrentStructure = null;
                            cx.Mode = UnifyMode.Read;
                            cx.S = 0;
                            cx.ClauseVars.Remove(-1);
                        }
                    }
                }
            }
        }
        else
        {
            // READ mode: Unify with value at S position
            if (cx.CurrentStructure is StructTerm struc)
            {
                if (cx.S < struc.Args.Count)
                {
                    object? value = struc.Args[cx.S];

                    // Per spec v2.16.3: Dereference VarRef pointing to value cell
                    if (value is VarRef vvc && cx.Rt.Heap.IsValue(vvc.Addr))
                    {
                        value = cx.Rt.Heap.GetValue(vvc.Addr)!;
                    }

                    cx.ClauseVars.TryGetValue(varIndex, out var existingValue);

                    if (isReaderMode)
                    {
                        // UnifyReader READ mode logic
                        if (value is VarRef vr && cx.Rt.Heap.IsReader(vr.Addr))
                        {
                            // Reader × Reader = FAIL
                            _softFailToNextClause(cx, pc);
                            return _Step.Jump(_findNextClauseTry(pc));
                        }
                        else if (value is VarRef vw && cx.Rt.Heap.IsWriter(vw.Addr))
                        {
                            // Query has writer, clause expects reader
                            if (existingValue != null)
                            {
                                if (existingValue is ConstTerm || existingValue is StructTerm)
                                {
                                    cx.SigmaHat[vw.Addr] = existingValue;
                                }
                                else if (existingValue is VarRef evr)
                                {
                                    var addr = evr.Addr;
                                    var readerAddr = cx.Rt.Heap.IsWriter(addr) ? cx.Rt.Heap.PairedReaderAddr(addr) : addr;
                                    cx.SigmaHat[vw.Addr] = new VarRef(readerAddr);
                                }
                                else if (existingValue is int evi)
                                {
                                    cx.SigmaHat[vw.Addr] = new VarRef(cx.Rt.Heap.PairedReaderAddr(evi));
                                }
                                cx.S++;
                            }
                            else
                            {
                                // First occurrence: head reader receives goal writer
                                cx.ClauseVars[varIndex] = vw.Addr;
                                cx.S++;
                            }
                        }
                        else if (value is ConstTerm || value is StructTerm)
                        {
                            // Query has ground term, clause expects reader
                            var (writerAddr, _) = cx.Rt.Heap.AllocateVariable();
                            cx.SigmaHat[writerAddr] = (Term)value;
                            cx.ClauseVars[varIndex] = writerAddr;
                            cx.S++;
                        }
                        else
                        {
                            _softFailToNextClause(cx, pc);
                            return _Step.Jump(_findNextClauseTry(pc));
                        }
                    }
                    else
                    {
                        // UnifyWriter READ mode logic
                        if (existingValue is int || (existingValue is VarRef evw && cx.Rt.Heap.IsWriter(evw.Addr)))
                        {
                            var clauseVarAddr = existingValue is int evi ? evi : ((VarRef)existingValue!).Addr;

                            if (value is VarRef vw && cx.Rt.Heap.IsWriter(vw.Addr))
                            {
                                // Query has writer - check for WxW violation
                                var clauseVarBound = cx.Rt.Heap.IsWriterBound(clauseVarAddr);
                                var queryVarBound = cx.Rt.Heap.IsWriterBound(vw.Addr);
                                if (!clauseVarBound && !queryVarBound)
                                {
                                    _softFailToNextClause(cx, pc);
                                    return _Step.Jump(_findNextClauseTry(pc));
                                }
                                cx.SigmaHat[clauseVarAddr] = value;
                                cx.S++;
                            }
                            else if (value is VarRef vr && cx.Rt.Heap.IsReader(vr.Addr))
                            {
                                cx.SigmaHat[clauseVarAddr] = value;
                                cx.S++;
                            }
                            else if (value is ConstTerm || value is StructTerm)
                            {
                                cx.SigmaHat[clauseVarAddr] = value;
                                cx.S++;
                            }
                            else
                            {
                                _softFailToNextClause(cx, pc);
                                return _Step.Jump(_findNextClauseTry(pc));
                            }
                        }
                        else if (existingValue != null)
                        {
                            // Clause variable already bound - advance
                            cx.S++;
                        }
                        else
                        {
                            // First occurrence - store the value
                            if (value is VarRef vw && cx.Rt.Heap.IsWriter(vw.Addr))
                            {
                                cx.ClauseVars[varIndex] = value;
                                cx.S++;
                            }
                            else if (value is VarRef vr && cx.Rt.Heap.IsReader(vr.Addr))
                            {
                                var rid = vr.Addr;
                                if (cx.Rt.Heap.IsReaderBound(rid))
                                {
                                    var readerValue = cx.Rt.Heap.GetReaderValue(rid);
                                    cx.ClauseVars[varIndex] = readerValue;
                                }
                                else
                                {
                                    cx.ClauseVars[varIndex] = value;
                                }
                                cx.S++;
                            }
                            else if (value is ConstTerm || value is StructTerm)
                            {
                                cx.ClauseVars[varIndex] = value;
                                cx.S++;
                            }
                            else
                            {
                                _softFailToNextClause(cx, pc);
                                return _Step.Jump(_findNextClauseTry(pc));
                            }
                        }
                    }
                }
            }
        }
        return _Step.Advance();
    }

    private _Step ExecV2Unknown(RunnerContext cx, V2.Unknown op)
    {
        var pc = cx.Pc;
        // Unknown: test if variable is unbound (value unknown)
        cx.ClauseVars.TryGetValue((int)op.VarIndex, out var term);
        // Succeeds if variable is unbound (no value yet)
        if (term is VarRef vr)
        {
            // Check if variable is unbound in σ̂w or heap
            if (cx.SigmaHat.ContainsKey(vr.Addr))
            {
                // Has tentative binding - not unknown
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
            if (cx.Rt.Heap.IsBound(vr.Addr))
            {
                // Has heap binding - not unknown
                _softFailToNextClause(cx, pc);
                return _Step.Jump(_findNextClauseTry(pc));
            }
            // Unbound = unknown, succeed
            return _Step.Advance();
        }
        // Non-variable is always known (bound to a value)
        _softFailToNextClause(cx, pc);
        return _Step.Jump(_findNextClauseTry(pc));
    }

    // ── Private helpers (filled — chunk 6) ───────────────────────────────────

    /// <summary>Dereference a term and track any unbound readers encountered (guard suspension detection).</summary>
    private static (object?, ISet<int>) _dereferenceWithTracking(object? term, RunnerContext cx)
    {
        var unboundReaders = new HashSet<int>();

        object? Dereference(object? t)
        {
            // Resolve clauseVars first (same pattern as Execute fix)
            if (t is VarRef tvr && cx.ClauseVars.ContainsKey(tvr.Addr))
            {
                // Resolve clause variable index to actual heap addr
                var resolved = cx.ClauseVars[tvr.Addr];
                if (resolved is int resolvedInt)
                {
                    t = new VarRef(resolvedInt);
                }
                else if (resolved != null)
                {
                    // Already resolved to a term
                    return Dereference(resolved);
                }
            }

            if (t is VarRef vr)
            {
                var addr = vr.Addr;
                if (cx.Rt.Heap.IsReader(addr))
                {
                    // Reader - check if bound using abstraction methods for imported reader support
                    var readerAddr = addr;

                    // Check sigma-hat first for tentative bindings (before commit)
                    var writerAddr = cx.Rt.Heap.TryWriterForReader(readerAddr);
                    if (writerAddr != null && cx.SigmaHat.ContainsKey(writerAddr.Value))
                    {
                        return Dereference(cx.SigmaHat[writerAddr.Value]);
                    }

                    if (cx.Rt.Heap.IsReaderBound(readerAddr))
                    {
                        var boundValue = cx.Rt.Heap.GetReaderValue(readerAddr);
                        // CRITICAL FIX: Recursively dereference the bound value
                        return Dereference(boundValue);
                    }
                    else
                    {
                        // Unbound reader - track it
                        unboundReaders.Add(readerAddr);
                        return t;
                    }
                }
                else
                {
                    // Writer variable
                    var writerAddr = addr;

                    // Check sigma-hat first (tentative bindings)
                    if (cx.SigmaHat.ContainsKey(writerAddr))
                    {
                        return Dereference(cx.SigmaHat[writerAddr]);
                    }

                    // Check heap
                    if (cx.Rt.Heap.IsFullyBound(writerAddr))
                    {
                        var boundValue = cx.Rt.Heap.GetValue(writerAddr);
                        // CRITICAL FIX: Recursively dereference the bound value
                        return Dereference(boundValue);
                    }
                    else
                    {
                        // Unbound writer - can't evaluate
                        return t;
                    }
                }
            }
            else if (t is StructTerm)
            {
                // FR-034/SC-009: a compound operand may hide a nested unbound reader
                // (e.g. peer(Region, Id?) with Id? un-arrived from a remote bind).
                // Recurse into the args so that reader is collected into
                // `unboundReaders` → the generic guard gate SUSPENDS on it, instead of
                // passing the struct through to be wrongly committed as a FAIL (a
                // non-monotone wrong commit; _termsEqual returns false on the unbound
                // inner reader). Mirrors the dedicated GroundEqual opcode's CollectUnbound.
                // The structure itself is still returned as-is — guards like =:= and the
                // term comparators re-deref the args via their own visited-set machinery.
                _collectUnboundReaders(t, cx, unboundReaders);
                return t;
            }
            else if (t is ConstTerm ct)
            {
                // CRITICAL FIX: Unwrap ConstTerm to get primitive value
                return ct.Value;
            }
            else if (t is int ti)
            {
                // Bare int represents a variable addr - check sigmaHat first, then heap
                if (cx.SigmaHat.ContainsKey(ti))
                {
                    return Dereference(cx.SigmaHat[ti]);
                }
                else if (cx.Rt.Heap.IsFullyBound(ti))
                {
                    var boundValue = cx.Rt.Heap.GetValue(ti);
                    // Recursively dereference the bound value
                    return Dereference(boundValue);
                }
                else
                {
                    // Unbound variable - return as VarRef for proper handling
                    return new VarRef(ti);
                }
            }
            else
            {
                return t;
            }
        }

        var result = Dereference(term);
        return (result, unboundReaders);
    }

    /// <summary>
    /// FR-034/SC-009: collect every unbound reader nested anywhere inside <paramref name="term"/>
    /// — compound args included — into <paramref name="outSet"/>. Used by the generic guard path
    /// so a nested un-arrived reader makes the guard SUSPEND (reactivate once on bind) rather than
    /// wrongly commit a FAIL. A bound writer/reader recurses into its value; an unbound reader is
    /// recorded; an unbound writer is left for the comparator to FAIL on (verdict matrix:
    /// reader→suspend, writer→fail). The address-keyed visited set guarantees termination on a
    /// cyclic compound. Structural mirror of the dedicated GroundEqual opcode's CollectUnbound.
    /// </summary>
    private static void _collectUnboundReaders(object? term, RunnerContext cx, ISet<int> outSet)
    {
        var visited = new HashSet<int>();
        void Walk(object? t)
        {
            if (t is VarRef wterm && cx.Rt.Heap.IsWriter(wterm.Addr))
            {
                var writerAddr = wterm.Addr;
                if (!visited.Add(writerAddr)) return;
                if (cx.SigmaHat.TryGetValue(writerAddr, out var sigmaBinding) && sigmaBinding != null)
                {
                    Walk(sigmaBinding);
                }
                else if (cx.Rt.Heap.IsFullyBound(writerAddr))
                {
                    Walk(cx.Rt.Heap.GetValue(writerAddr));
                }
                // Unbound writer: not a reader → not collected (comparator FAILs).
            }
            else if (t is VarRef rterm && cx.Rt.Heap.IsReader(rterm.Addr))
            {
                var readerAddr = rterm.Addr;
                if (!visited.Add(readerAddr)) return;
                if (cx.SigmaHat.TryGetValue(readerAddr, out var sigmaBinding) && sigmaBinding != null)
                {
                    Walk(sigmaBinding);
                }
                else if (!cx.Rt.Heap.IsReaderBound(readerAddr))
                {
                    outSet.Add(readerAddr);
                }
                else
                {
                    Walk(cx.Rt.Heap.GetReaderValue(readerAddr));
                }
            }
            else if (t is StructTerm st)
            {
                foreach (var arg in st.Args) Walk(arg);
            }
            else if (t is _TentativeStruct ts)
            {
                foreach (var arg in ts.Args) Walk(arg);
            }
            else if (t is int ti)
            {
                if (!visited.Add(ti)) return;
                if (cx.SigmaHat.TryGetValue(ti, out var sigmaBinding) && sigmaBinding != null)
                {
                    Walk(sigmaBinding);
                }
                else if (cx.Rt.Heap.IsWriter(ti))
                {
                    if (cx.Rt.Heap.IsFullyBound(ti))
                    {
                        Walk(cx.Rt.Heap.GetValue(ti));
                    }
                }
                else if (!cx.Rt.Heap.IsReaderBound(ti))
                {
                    outSet.Add(ti);
                }
                else
                {
                    Walk(cx.Rt.Heap.GetReaderValue(ti));
                }
            }
            // Constants and other leaves contribute nothing.
        }
        Walk(term);
    }

    /// <summary>Test if a functor is an arithmetic operator.</summary>
    private static bool _isArithmeticOp(string functor)
    {
        return functor is "+" or "-" or "*" or "/" or "mod" or "neg";
    }

    /// <summary>Evaluate an arithmetic expression (already ground).</summary>
    private static double _evaluateArithmetic(string op, IReadOnlyList<object?> args)
    {
        // Extract numeric values
        double GetNum(object? v)
        {
            if (v is double dv) return dv;
            if (v is int iv) return iv;
            if (v is long lv) return lv;
            if (v is ConstTerm cv)
            {
                if (cv.Value is double cdv) return cdv;
                if (cv.Value is int civ) return civ;
                if (cv.Value is long clv) return clv;
            }
            throw new InvalidOperationException($"Non-numeric value in arithmetic: {v}");
        }

        if (args.Count == 0)
        {
            throw new InvalidOperationException($"Arithmetic operator {op} requires arguments");
        }

        var a = GetNum(args[0]);

        // Unary operators
        if (op == "neg" || (op == "-" && args.Count == 1))
        {
            return -a;
        }

        // Binary operators
        if (args.Count < 2)
        {
            throw new InvalidOperationException($"Binary operator {op} requires two arguments");
        }
        var b = GetNum(args[1]);

        switch (op)
        {
            case "+": return a + b;
            case "-": return a - b;
            case "*": return a * b;
            case "/": return a / b;
            case "mod": return (long)a % (long)b;
            default: throw new InvalidOperationException($"Unknown arithmetic operator: {op}");
        }
    }

    /// <summary>Evaluate a guard predicate with ground arguments.</summary>
    private static GuardResult _evaluateGuard(string predicateName, IReadOnlyList<object?> args, RunnerContext cx)
    {
        // Extract values from any remaining ConstTerms
        object? GetValue(object? v)
        {
            if (v is ConstTerm ct) return ct.Value;
            return v;
        }

        // Evaluate arithmetic expressions to numeric values
        // Supports: X, X + Y, X - Y, X * Y, X / Y, X // Y, X mod Y, -X
        double? EvaluateNumeric(object? v)
        {
            if (v is double dv) return dv;
            if (v is int iv) return iv;
            if (v is long lv) return lv;
            if (v is ConstTerm cnum)
            {
                if (cnum.Value is double cdv) return cdv;
                if (cnum.Value is int civ) return civ;
                if (cnum.Value is long clv) return clv;
            }
            // Handle VarRef - dereference to get actual value
            if (v is VarRef vref)
            {
                if (cx.Rt.Heap.IsReader(vref.Addr))
                {
                    // Use isReaderBound/getReaderValue for imported reader support
                    if (!cx.Rt.Heap.IsReaderBound(vref.Addr)) return null; // Unbound
                    var deref = cx.Rt.Heap.GetReaderValue(vref.Addr);
                    return EvaluateNumeric(deref);
                }
                else
                {
                    var deref = cx.Rt.Heap.GetValue(vref.Addr);
                    if (deref == null) return null; // Unbound
                    return EvaluateNumeric(deref);
                }
            }
            if (v is StructTerm st)
            {
                // Evaluate arithmetic expression
                switch (st.Functor)
                {
                    case "+":
                        if (st.Args.Count != 2) return null;
                        {
                            var a = EvaluateNumeric(st.Args[0]);
                            var b = EvaluateNumeric(st.Args[1]);
                            if (a == null || b == null) return null;
                            return a + b;
                        }
                    case "-":
                        if (st.Args.Count == 1)
                        {
                            // Unary minus
                            var a = EvaluateNumeric(st.Args[0]);
                            return a == null ? null : -a;
                        }
                        else if (st.Args.Count == 2)
                        {
                            var a = EvaluateNumeric(st.Args[0]);
                            var b = EvaluateNumeric(st.Args[1]);
                            if (a == null || b == null) return null;
                            return a - b;
                        }
                        return null;
                    case "*":
                        if (st.Args.Count != 2) return null;
                        {
                            var a = EvaluateNumeric(st.Args[0]);
                            var b = EvaluateNumeric(st.Args[1]);
                            if (a == null || b == null) return null;
                            return a * b;
                        }
                    case "/":
                        if (st.Args.Count != 2) return null;
                        {
                            var a = EvaluateNumeric(st.Args[0]);
                            var b = EvaluateNumeric(st.Args[1]);
                            if (a == null || b == null || b == 0) return null;
                            return a / b;
                        }
                    case "//":
                        if (st.Args.Count != 2) return null;
                        {
                            var a = EvaluateNumeric(st.Args[0]);
                            var b = EvaluateNumeric(st.Args[1]);
                            if (a == null || b == null || b == 0) return null;
                            return Math.Truncate(a.Value / b.Value);
                        }
                    case "mod":
                        if (st.Args.Count != 2) return null;
                        {
                            var a = EvaluateNumeric(st.Args[0]);
                            var b = EvaluateNumeric(st.Args[1]);
                            if (a == null || b == null || b == 0) return null;
                            return (long)a.Value % (long)b.Value;
                        }
                    case "neg":
                        if (st.Args.Count != 1) return null;
                        {
                            var a = EvaluateNumeric(st.Args[0]);
                            return a == null ? null : -a;
                        }
                    default:
                        return null; // Not an arithmetic functor
                }
            }
            return null;
        }

        switch (predicateName)
        {
            // Comparison guards (with arithmetic expression support)
            case "<":
                if (args.Count < 2) return GuardResult.Fail;
                {
                    var a = EvaluateNumeric(args[0]);
                    var b = EvaluateNumeric(args[1]);
                    if (a != null && b != null)
                    {
                        return a < b ? GuardResult.Pass : GuardResult.Fail;
                    }
                    return GuardResult.Fail;
                }

            case ">":
                if (args.Count < 2) return GuardResult.Fail;
                {
                    var a = EvaluateNumeric(args[0]);
                    var b = EvaluateNumeric(args[1]);
                    if (a != null && b != null)
                    {
                        return a > b ? GuardResult.Pass : GuardResult.Fail;
                    }
                    return GuardResult.Fail;
                }

            case "=<":
                if (args.Count < 2) return GuardResult.Fail;
                {
                    var a = EvaluateNumeric(args[0]);
                    var b = EvaluateNumeric(args[1]);
                    if (a != null && b != null)
                    {
                        return a <= b ? GuardResult.Pass : GuardResult.Fail;
                    }
                    return GuardResult.Fail;
                }

            case ">=":
                if (args.Count < 2) return GuardResult.Fail;
                {
                    var a = EvaluateNumeric(args[0]);
                    var b = EvaluateNumeric(args[1]);
                    if (a != null && b != null)
                    {
                        return a >= b ? GuardResult.Pass : GuardResult.Fail;
                    }
                    return GuardResult.Fail;
                }

            case "=:=":
                if (args.Count < 2) return GuardResult.Fail;
                {
                    var a = EvaluateNumeric(args[0]);
                    var b = EvaluateNumeric(args[1]);
                    if (a != null && b != null)
                    {
                        return a == b ? GuardResult.Pass : GuardResult.Fail;
                    }
                    return GuardResult.Fail;
                }

            case "=\\=":
                if (args.Count < 2) return GuardResult.Fail;
                {
                    var a = EvaluateNumeric(args[0]);
                    var b = EvaluateNumeric(args[1]);
                    if (a != null && b != null)
                    {
                        return a != b ? GuardResult.Pass : GuardResult.Fail;
                    }
                    return GuardResult.Fail;
                }

            // Type guards
            case "ground":
                // Already checked for unbound readers in caller
                return GuardResult.Pass;

            case "known":
                // Check if argument is not a variable
                if (args.Count == 0) return GuardResult.Fail;
                {
                    var arg = args[0];
                    if (arg is VarRef)
                    {
                        return GuardResult.Fail;
                    }
                    return GuardResult.Pass;
                }

            case "integer":
                // Per spec 19.4.3: Test if Xi is an integer
                if (args.Count == 0) return GuardResult.Fail;
                {
                    var val = GetValue(args[0]);
                    return (val is int || val is long) ? GuardResult.Pass : GuardResult.Fail;
                }

            // FR-033/SC-005 (OQ-G3 RULED): `atom/1` is the paper-kernel name and an
            // EXACT synonym of the runtime `string/1` test — a non-numeric atomic
            // constant, excluding `[]`/`nil`. Stacked label so both share one body.
            case "atom":
            case "string":
                // Succeeds if X is a string (lowercase identifier or quoted string)
                if (args.Count == 0) return GuardResult.Fail;
                {
                    var val = GetValue(args[0]);
                    // String: ConstTerm with String value (not 'nil' which represents [])
                    if (val is ConstTerm cstr && cstr.Value is string cs && cs != "nil")
                    {
                        return GuardResult.Pass;
                    }
                    if (val is string s && s != "nil")
                    {
                        return GuardResult.Pass;
                    }
                    return GuardResult.Fail;
                }

            case "constant":
                // Succeeds if X is a constant (a string, a number, or [])
                if (args.Count == 0) return GuardResult.Fail;
                {
                    var val = GetValue(args[0]);
                    // String or nil (which represents [])
                    if (val is ConstTerm cc && cc.Value is string)
                    {
                        return GuardResult.Pass;
                    }
                    if (val is string)
                    {
                        return GuardResult.Pass;
                    }
                    // Number
                    if (val is int || val is long || val is double)
                    {
                        return GuardResult.Pass;
                    }
                    if (val is ConstTerm cn && (cn.Value is int || cn.Value is long || cn.Value is double))
                    {
                        return GuardResult.Pass;
                    }
                    return GuardResult.Fail;
                }

            case "number":
                // Succeeds if X is a number
                if (args.Count == 0) return GuardResult.Fail;
                {
                    var val = GetValue(args[0]);
                    if (val is int || val is long || val is double) return GuardResult.Pass;
                    if (val is ConstTerm cn && (cn.Value is int || cn.Value is long || cn.Value is double)) return GuardResult.Pass;
                    return GuardResult.Fail;
                }

            case "list":
            case "is_list":
                // Succeeds if X is a list ([] or [H|T])
                if (args.Count == 0) return GuardResult.Fail;
                {
                    var val = GetValue(args[0]);
                    // Empty list: ConstTerm('nil') / null, or raw String 'nil'
                    if (val is ConstTerm cnil && (Equals(cnil.Value, "nil") || cnil.Value == null))
                    {
                        return GuardResult.Pass;
                    }
                    if (val is string ls && ls == "nil")
                    {
                        return GuardResult.Pass;
                    }
                    // Non-empty list / cons cell: StructTerm('.', ...)
                    if (val is StructTerm lst && lst.Functor == ".")
                    {
                        return GuardResult.Pass;
                    }
                    return GuardResult.Fail;
                }

            case "compound":
            case "tuple":
                // Succeeds if X is a compound term (structure with functor and arity > 0)
                // Lists are compound since [X|Xs] = '.'(X, Xs)
                // Does NOT imply groundness - may contain unbound subterms
                // 'tuple' is a book-terminology synonym for 'compound' (per AoGLP 2025).
                if (args.Count == 0) return GuardResult.Fail;
                {
                    var val = GetValue(args[0]);
                    if (val is StructTerm cst && cst.Args.Count > 0)
                    {
                        return GuardResult.Pass;
                    }
                    return GuardResult.Fail;
                }

            case "module":
                // Succeeds if X is a ModuleTerm (ground module reference)
                if (args.Count == 0) return GuardResult.Fail;
                {
                    var mval = GetValue(args[0]);
                    if (mval is ModuleTerm)
                    {
                        return GuardResult.Pass;
                    }
                    return GuardResult.Fail;
                }

            case "is_mutual_ref":
                // Succeeds if X is a MutualRefTerm (enables SRSW multiple reads)
                if (args.Count == 0) return GuardResult.Fail;
                {
                    var val = GetValue(args[0]);
                    if (val is MutualRefTerm)
                    {
                        return GuardResult.Pass;
                    }
                    return GuardResult.Fail;
                }

            case "unknown":
                // Test if dereferencing leads to an unbound variable
                // Per spec: "Succeeds if X is bound to an unbound variable"
                // This means we follow the binding chain to its end
                if (args.Count == 0) return GuardResult.Fail;
                {
                    object? value = args[0];

                    // Follow binding chain to end
                    while (value is VarRef vvr)
                    {
                        var addr = vvr.Addr;
                        if (cx.Rt.Heap.IsReader(addr))
                        {
                            // Use abstraction methods for imported reader support
                            var writerAddr = cx.Rt.Heap.TryWriterForReader(addr);
                            if (writerAddr != null && cx.SigmaHat.ContainsKey(writerAddr.Value))
                            {
                                value = cx.SigmaHat[writerAddr.Value];
                                continue;
                            }
                            // Check heap using isReaderBound/getReaderValue
                            if (cx.Rt.Heap.IsReaderBound(addr))
                            {
                                value = cx.Rt.Heap.GetReaderValue(addr);
                                continue;
                            }
                            // Reached an unbound reader → SUCCESS
                            return GuardResult.Pass;
                        }
                        else
                        {
                            // Writer - check σ̂w first, then heap
                            if (cx.SigmaHat.ContainsKey(addr))
                            {
                                value = cx.SigmaHat[addr];
                                continue;
                            }
                            if (cx.Rt.Heap.IsFullyBound(addr))
                            {
                                value = cx.Rt.Heap.GetValue(addr);
                                continue;
                            }
                            // Reached an unbound writer → SUCCESS
                            return GuardResult.Pass;
                        }
                    }
                    // Dereferenced to a non-variable (ground term) → FAILURE
                    return GuardResult.Fail;
                }

            // Control guards
            case "otherwise":
                // This is handled by the compiler - should not reach runtime
                return GuardResult.Pass;

            // Time guards
            case "wait":
                // wait(Duration) - Wait for Duration milliseconds using GLP suspension
                if (args.Count == 0) return GuardResult.Fail;
                {
                    var duration = EvaluateNumeric(args[0]);
                    if (duration == null) return GuardResult.Fail;
                    if (duration <= 0) return GuardResult.Pass;

                    // Check if this goal already has a pending wait
                    var existingReader = cx.Rt.GetWaitReader(cx.GoalId);
                    if (existingReader != null)
                    {
                        // Goal resumed after suspension - check if timer fired
                        if (cx.Rt.Heap.IsFullyBound(existingReader.Value))
                        {
                            // Timer fired, reader is bound - clear state and succeed
                            cx.Rt.ClearWaitState(cx.GoalId);
                            return GuardResult.Pass;
                        }
                        else
                        {
                            // Timer hasn't fired yet - keep suspending on same reader
                            cx.U.Add(existingReader.Value);
                            return GuardResult.Fail;
                        }
                    }

                    // First call - create fresh reader/writer pair for timer notification
                    var (writerAddr, readerAddr) = cx.Rt.Heap.AllocateVariable();

                    // Store wait state for this goal
                    cx.Rt.SetWaitReader(cx.GoalId, readerAddr);

                    // Track pending timer
                    cx.Rt.IncrementPendingTimers();

                    // Start timer that binds writer when it fires
                    _startGlpTimer((int)duration.Value, cx.Rt, writerAddr);

                    // Add reader to suspension set U and fail → triggers normal suspension
                    cx.U.Add(readerAddr);
                    return GuardResult.Fail;
                }

            case "wait_until":
                // wait_until(Timestamp) - Suspend until absolute time has passed
                if (args.Count == 0) return GuardResult.Fail;
                {
                    var timestamp = EvaluateNumeric(args[0]);
                    if (timestamp == null) return GuardResult.Fail;
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (now >= timestamp) return GuardResult.Pass;

                    // Time hasn't arrived yet — use timer-based suspension (same as wait)
                    var remaining = (int)(timestamp.Value - now);

                    // Check if this goal already has a pending wait_until
                    var existingReaderWU = cx.Rt.GetWaitReader(cx.GoalId);
                    if (existingReaderWU != null)
                    {
                        if (cx.Rt.Heap.IsFullyBound(existingReaderWU.Value))
                        {
                            cx.Rt.ClearWaitState(cx.GoalId);
                            return GuardResult.Pass;
                        }
                        else
                        {
                            cx.U.Add(existingReaderWU.Value);
                            return GuardResult.Fail;
                        }
                    }

                    // First call — create fresh reader/writer pair for timer notification
                    var (writerAddrWU, readerAddrWU) = cx.Rt.Heap.AllocateVariable();
                    cx.Rt.SetWaitReader(cx.GoalId, readerAddrWU);
                    cx.Rt.IncrementPendingTimers();

                    _startGlpTimer(remaining, cx.Rt, writerAddrWU);

                    cx.U.Add(readerAddrWU);
                    return GuardResult.Fail;
                }

            case "=?=":
                // Ground equality test
                if (args.Count < 2) return GuardResult.Fail;
                {
                    var left = args[0];
                    var right = args[1];

                    // Check for unbound writers (VarRef that reached here is unbound writer)
                    // Unbound readers would have caused suspension in caller
                    if (left is VarRef || right is VarRef)
                    {
                        return GuardResult.Fail; // Unbound writer → fail
                    }

                    // Both ground - check structural equality
                    var result = _termsEqual(left, right, cx);
                    return result ? GuardResult.Pass : GuardResult.Fail;
                }

            default:
                Console.WriteLine($"[WARN] Unknown guard predicate: {predicateName}");
                return GuardResult.Fail;
        }
    }

    /// <summary>
    /// Start a GLP suspension timer: after <paramref name="ms"/> milliseconds, bind the writer
    /// and re-enqueue any reactivated goals. Mirrors the Dart dart:async Timer callback.
    /// </summary>
    private static void _startGlpTimer(int ms, GlpRuntimeEngine rt, int writerAddr)
    {
        System.Threading.Timer? timer = null;
        timer = new System.Threading.Timer(_ =>
        {
            // Bind writer to 0 (any value works)
            var reactivated = rt.Heap.BindWriterConst(writerAddr, 0);
            // Enqueue reactivated goals and clean up suspended map
            foreach (var goalRef in reactivated)
            {
                rt.EnqueueReactivatedGoal(goalRef);
            }
            // Decrement pending timer count
            rt.DecrementPendingTimers();
            timer?.Dispose();
        }, null, ms < 0 ? 0 : ms, System.Threading.Timeout.Infinite);
    }

    /// <summary>Check structural equality of two ground terms (with cycle detection).</summary>
    private static bool _termsEqual(object? a, object? b, RunnerContext cx, HashSet<(int, int)>? visited = null)
    {
        visited ??= new HashSet<(int, int)>();

        // Handle null
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        // Unwrap ConstTerm
        if (a is ConstTerm act) a = act.Value;
        if (b is ConstTerm bct) b = bct.Value;
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        // Dereference VarRefs with cycle detection
        if (a is VarRef avr)
        {
            var aAddr = avr.Addr;
            object? aDeref;
            if (cx.Rt.Heap.IsReader(aAddr))
            {
                // Use abstraction methods for imported reader support
                var writerAddr = cx.Rt.Heap.TryWriterForReader(aAddr);
                if (writerAddr != null && cx.SigmaHat.ContainsKey(writerAddr.Value))
                {
                    aDeref = cx.SigmaHat[writerAddr.Value];
                }
                else if (cx.Rt.Heap.IsReaderBound(aAddr))
                {
                    aDeref = cx.Rt.Heap.GetReaderValue(aAddr);
                }
                else
                {
                    return false; // Unbound - can't compare
                }
            }
            else
            {
                if (cx.SigmaHat.ContainsKey(aAddr))
                {
                    aDeref = cx.SigmaHat[aAddr];
                }
                else if (cx.Rt.Heap.IsFullyBound(aAddr))
                {
                    aDeref = cx.Rt.Heap.GetValue(aAddr);
                }
                else
                {
                    return false; // Unbound writer
                }
            }

            // If b is also a VarRef, check for cycle
            if (b is VarRef bvr0)
            {
                var bAddr = bvr0.Addr;
                var pair = (aAddr, bAddr);
                if (visited.Contains(pair))
                {
                    return true; // Cycle detected at corresponding positions - equal
                }
                visited.Add(pair);
            }

            return _termsEqual(aDeref, b, cx, visited);
        }
        if (b is VarRef bvr)
        {
            var bAddr = bvr.Addr;
            object? bDeref;
            if (cx.Rt.Heap.IsReader(bAddr))
            {
                // Use abstraction methods for imported reader support
                var writerAddr = cx.Rt.Heap.TryWriterForReader(bAddr);
                if (writerAddr != null && cx.SigmaHat.ContainsKey(writerAddr.Value))
                {
                    bDeref = cx.SigmaHat[writerAddr.Value];
                }
                else if (cx.Rt.Heap.IsReaderBound(bAddr))
                {
                    bDeref = cx.Rt.Heap.GetReaderValue(bAddr);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (cx.SigmaHat.ContainsKey(bAddr))
                {
                    bDeref = cx.SigmaHat[bAddr];
                }
                else if (cx.Rt.Heap.IsFullyBound(bAddr))
                {
                    bDeref = cx.Rt.Heap.GetValue(bAddr);
                }
                else
                {
                    return false;
                }
            }
            return _termsEqual(a, bDeref, cx, visited);
        }

        // Simple values (numbers, strings)
        if (_isNum(a) && _isNum(b)) return _numEquals(a, b);
        if (a is string astr && b is string bstr) return astr == bstr;

        // Structures
        if (a is StructTerm asTerm && b is StructTerm bsTerm)
        {
            if (asTerm.Functor != bsTerm.Functor) return false;
            if (asTerm.Args.Count != bsTerm.Args.Count) return false;
            for (var i = 0; i < asTerm.Args.Count; i++)
            {
                if (!_termsEqual(asTerm.Args[i], bsTerm.Args[i], cx, visited)) return false;
            }
            return true;
        }

        // Default: use object equality
        return Equals(a, b);
    }

    /// <summary>True if value is a GLP numeric (int/long/double).</summary>
    private static bool _isNum(object? v) => v is int || v is long || v is double;

    /// <summary>Numeric equality across int/long/double (Dart num == semantics).</summary>
    private static bool _numEquals(object? a, object? b)
    {
        double Conv(object? v) => v is double d ? d : v is long l ? l : v is int i ? i : double.NaN;
        return Conv(a) == Conv(b);
    }

    /// <summary>Recursively convert a _TentativeStruct to a StructTerm.</summary>
    private static StructTerm _convertTentativeToStruct(_TentativeStruct tentative, RunnerContext cx)
    {
        var termArgs = new List<Term>();
        foreach (var arg in tentative.Args)
        {
            if (arg is _TentativeStruct nested)
            {
                // Recursively convert nested tentative structures
                termArgs.Add(_convertTentativeToStruct(nested, cx));
            }
            else if (arg is Term argTerm)
            {
                // Already a Term - use as-is
                termArgs.Add(argTerm);
            }
            else if (arg == null)
            {
                // Null -> ConstTerm(null)
                termArgs.Add(new ConstTerm(null));
            }
            else
            {
                // Raw value -> ConstTerm
                termArgs.Add(new ConstTerm(arg));
            }
        }
        return new StructTerm(tentative.Functor, termArgs);
    }
}

// ── Tail helper classes (fully implemented — fill chunks depend on their fields) ──

/// <summary>Helper class to represent argument information.</summary>
internal sealed class _ArgInfo
{
    public int? WriterId { get; }
    public int? ReaderId { get; }

    public _ArgInfo(int? writerId = null, int? readerId = null)
    {
        WriterId = writerId;
        ReaderId = readerId;
    }

    public bool IsWriter => WriterId != null;
    public bool IsReader => ReaderId != null;
}

/// <summary>Tentative structure during HEAD phase (before commit).</summary>
internal sealed class _TentativeStruct
{
    public string Functor { get; }
    public int Arity { get; }
    public List<object?> Args { get; }

    public _TentativeStruct(string functor, int arity, List<object?> args)
    {
        Functor = functor;
        Arity = arity;
        Args = args;
    }

    public override string ToString() => $"{Functor}/{Arity}({string.Join(", ", Args)})";
}

/// <summary>Represents a clause variable (before actual binding).</summary>
internal sealed class _ClauseVar
{
    public int VarIndex { get; }
    public bool IsWriter { get; }

    public _ClauseVar(int varIndex, bool isWriter)
    {
        VarIndex = varIndex;
        IsWriter = isWriter;
    }

    public override string ToString() => IsWriter ? $"W{VarIndex}" : $"R{VarIndex}";
}

/// <summary>Represents a list structure (head|tail) during HEAD building.</summary>
internal sealed class _ListStruct
{
    public object? Head { get; }
    public object? Tail { get; }

    public _ListStruct(object? head, object? tail)
    {
        Head = head;
        Tail = tail;
    }

    public override string ToString() => $"[{Head}|{Tail}]";
}

/// <summary>Saves/restores structure-processing state for Push/Pop.</summary>
internal sealed class _StructureState
{
    public int S { get; }
    public UnifyMode Mode { get; }
    public object? CurrentStructure { get; }

    public _StructureState(int s, UnifyMode mode, object? currentStructure)
    {
        S = s;
        Mode = mode;
        CurrentStructure = currentStructure;
    }

    public override string ToString() => $"StructureState(S={S}, mode={Mode}, struct={CurrentStructure})";
}
