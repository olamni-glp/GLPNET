// WAM/FCP bytecode interpreter — Dart→C# conversion of glp_runtime/lib/bytecode/runner.dart.
// CHUNK 1 of 6 (escalation E1): complete COMPILING SKELETON.
//   - Support types (BytecodeProgram, CallEnv, EnvironmentFrame, _ParentContext,
//     RunnerContext, _ArgInfo, _TentativeStruct, _ClauseVar, _ListStruct,
//     _StructureState) are FULLY implemented.
//   - The dispatch loop (RunStep → runWithStatus) is implemented: pc advance, mode,
//     reduction countdown, terminate/suspend/yield are real.
//   - Each opcode arm is a private Exec<OpName>(RunnerContext, <OpType>) method,
//     STUBBED (throws NotImplementedException). Later chunks fill the bodies.
//   - Private helpers (_evaluateGuard, _dereferenceWithTracking, _evaluateArithmetic,
//     _termsEqual, _convertTentativeToStruct) are stubbed.
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
        => throw new NotImplementedException("runner arm: Allocate");

    private _Step ExecBodySetConst(RunnerContext cx, BodySetConst op)
        => throw new NotImplementedException("runner arm: BodySetConst");

    private _Step ExecBodySetConstArg(RunnerContext cx, BodySetConstArg op)
        => throw new NotImplementedException("runner arm: BodySetConstArg");

    private _Step ExecBodySetStructConstArgs(RunnerContext cx, BodySetStructConstArgs op)
        => throw new NotImplementedException("runner arm: BodySetStructConstArgs");

    private _Step ExecClauseNext(RunnerContext cx, ClauseNext op)
        => throw new NotImplementedException("runner arm: ClauseNext");

    private _Step ExecClauseTry(RunnerContext cx, ClauseTry op)
        => throw new NotImplementedException("runner arm: ClauseTry");

    private _Step ExecCommit(RunnerContext cx, Commit op)
        => throw new NotImplementedException("runner arm: Commit");

    private _Step ExecDeallocate(RunnerContext cx, Deallocate op)
        => throw new NotImplementedException("runner arm: Deallocate");

    private _Step ExecDistribute(RunnerContext cx, Distribute op)
        => throw new NotImplementedException("runner arm: Distribute");

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
        => throw new NotImplementedException("runner arm: Ground");

    private _Step ExecGroundEqual(RunnerContext cx, GroundEqual op)
        => throw new NotImplementedException("runner arm: GroundEqual");

    private _Step ExecGuard(RunnerContext cx, Guard op)
        => throw new NotImplementedException("runner arm: Guard");

    private _Step ExecGuardFail(RunnerContext cx, GuardFail op)
        => throw new NotImplementedException("runner arm: GuardFail");

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
        => throw new NotImplementedException("runner arm: Halt");

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
                else if (value is ConstTerm ct && !Equals(ct.Value, op.Value))
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
                if (value is ConstTerm ct && !Equals(ct.Value, op.Value))
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
        => throw new NotImplementedException("runner arm: Known");

    private _Step ExecLabel(RunnerContext cx, Label op)
        => throw new NotImplementedException("runner arm: Label");

    private _Step ExecNoMoreClauses(RunnerContext cx, NoMoreClauses op)
        => throw new NotImplementedException("runner arm: NoMoreClauses");

    private _Step ExecNoReaders(RunnerContext cx, NoReaders op)
        => throw new NotImplementedException("runner arm: NoReaders");

    private _Step ExecNop(RunnerContext cx, Nop op)
        => throw new NotImplementedException("runner arm: Nop");

    private _Step ExecOtherwise(RunnerContext cx, Otherwise op)
        => throw new NotImplementedException("runner arm: Otherwise");

    private _Step ExecPop(RunnerContext cx, Pop op)
        => throw new NotImplementedException("runner arm: Pop");

    private _Step ExecProceed(RunnerContext cx, Proceed op)
        => throw new NotImplementedException("runner arm: Proceed");

    private _Step ExecPush(RunnerContext cx, Push op)
        => throw new NotImplementedException("runner arm: Push");

    private _Step ExecPutBoundConst(RunnerContext cx, PutBoundConst op)
        => throw new NotImplementedException("runner arm: PutBoundConst");

    private _Step ExecPutBoundNil(RunnerContext cx, PutBoundNil op)
        => throw new NotImplementedException("runner arm: PutBoundNil");

    private _Step ExecPutConstant(RunnerContext cx, PutConstant op)
        => throw new NotImplementedException("runner arm: PutConstant");

    private _Step ExecPutList(RunnerContext cx, PutList op)
        => throw new NotImplementedException("runner arm: PutList");

    private _Step ExecPutNil(RunnerContext cx, PutNil op)
        => throw new NotImplementedException("runner arm: PutNil");

    private _Step ExecPutStructure(RunnerContext cx, PutStructure op)
        => throw new NotImplementedException("runner arm: PutStructure");

    private _Step ExecRequeue(RunnerContext cx, Requeue op)
        => throw new NotImplementedException("runner arm: Requeue");

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
        => throw new NotImplementedException("runner arm: ResetAndGoto");

    private _Step ExecSetConstant(RunnerContext cx, SetConstant op)
        => throw new NotImplementedException("runner arm: SetConstant");

    private _Step ExecSpawn(RunnerContext cx, Spawn op)
        => throw new NotImplementedException("runner arm: Spawn");

    private _Step ExecSuspendEnd(RunnerContext cx, SuspendEnd op)
        => throw new NotImplementedException("runner arm: SuspendEnd");

    private _Step ExecTailStep(RunnerContext cx, TailStep op)
        => throw new NotImplementedException("runner arm: TailStep");

    private _Step ExecTransmit(RunnerContext cx, Transmit op)
        => throw new NotImplementedException("runner arm: Transmit");

    private _Step ExecTryNextClause(RunnerContext cx, TryNextClause op)
        => throw new NotImplementedException("runner arm: TryNextClause");

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

                    if (value is ConstTerm vc && Equals(vc.Value, op.Value))
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
                            if (boundValue is ConstTerm bc && Equals(bc.Value, op.Value))
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
                            if (boundValue is ConstTerm bc && Equals(bc.Value, op.Value))
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
        => throw new NotImplementedException("runner arm: UnionSiAndGoto");

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
                                match = Equals(avc.Value, svc.Value);
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
                            match = Equals(avc.Value, svc.Value);
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
                else if (storedValue is ConstTerm svc && !Equals(svc.Value, ac.Value))
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

    // ── Private helpers (STUBBED — filled by later chunks) ───────────────────

    /// <summary>Dereference a term and track any unbound readers encountered (guard suspension detection).</summary>
    private static (object?, ISet<int>) _dereferenceWithTracking(object? term, RunnerContext cx)
        => throw new NotImplementedException("runner helper: _dereferenceWithTracking");

    /// <summary>Test if a functor is an arithmetic operator.</summary>
    private static bool _isArithmeticOp(string functor)
        => throw new NotImplementedException("runner helper: _isArithmeticOp");

    /// <summary>Evaluate an arithmetic expression (already ground).</summary>
    private static double _evaluateArithmetic(string op, IReadOnlyList<object?> args)
        => throw new NotImplementedException("runner helper: _evaluateArithmetic");

    /// <summary>Evaluate a guard predicate with ground arguments.</summary>
    private static GuardResult _evaluateGuard(string predicateName, IReadOnlyList<object?> args, RunnerContext cx)
        => throw new NotImplementedException("runner helper: _evaluateGuard");

    /// <summary>Check structural equality of two ground terms (with cycle detection).</summary>
    private static bool _termsEqual(object? a, object? b, RunnerContext cx, HashSet<(int, int)>? visited = null)
        => throw new NotImplementedException("runner helper: _termsEqual");

    /// <summary>Recursively convert a _TentativeStruct to a StructTerm.</summary>
    private static StructTerm _convertTentativeToStruct(_TentativeStruct tentative, RunnerContext cx)
        => throw new NotImplementedException("runner helper: _convertTentativeToStruct");
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
