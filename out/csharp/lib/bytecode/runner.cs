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
        => throw new NotImplementedException("runner arm: GetValue");

    private _Step ExecGetVariable(RunnerContext cx, GetVariable op)
        => throw new NotImplementedException("runner arm: GetVariable");

    private _Step ExecGround(RunnerContext cx, Ground op)
        => throw new NotImplementedException("runner arm: Ground");

    private _Step ExecGroundEqual(RunnerContext cx, GroundEqual op)
        => throw new NotImplementedException("runner arm: GroundEqual");

    private _Step ExecGuard(RunnerContext cx, Guard op)
        => throw new NotImplementedException("runner arm: Guard");

    private _Step ExecGuardFail(RunnerContext cx, GuardFail op)
        => throw new NotImplementedException("runner arm: GuardFail");

    private _Step ExecGuardNeedReader(RunnerContext cx, GuardNeedReader op)
        => throw new NotImplementedException("runner arm: GuardNeedReader");

    private _Step ExecGuardNeedReaderArg(RunnerContext cx, GuardNeedReaderArg op)
        => throw new NotImplementedException("runner arm: GuardNeedReaderArg");

    private _Step ExecHalt(RunnerContext cx, Halt op)
        => throw new NotImplementedException("runner arm: Halt");

    private _Step ExecHeadBindWriter(RunnerContext cx, HeadBindWriter op)
        => throw new NotImplementedException("runner arm: HeadBindWriter");

    private _Step ExecHeadBindWriterArg(RunnerContext cx, HeadBindWriterArg op)
        => throw new NotImplementedException("runner arm: HeadBindWriterArg");

    private _Step ExecHeadConstant(RunnerContext cx, HeadConstant op)
        => throw new NotImplementedException("runner arm: HeadConstant");

    private _Step ExecHeadList(RunnerContext cx, HeadList op)
        => throw new NotImplementedException("runner arm: HeadList");

    private _Step ExecHeadNil(RunnerContext cx, HeadNil op)
        => throw new NotImplementedException("runner arm: HeadNil");

    private _Step ExecHeadStructure(RunnerContext cx, HeadStructure op)
        => throw new NotImplementedException("runner arm: HeadStructure");

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
        => throw new NotImplementedException("runner arm: RequireReaderArg");

    private _Step ExecRequireWriterArg(RunnerContext cx, RequireWriterArg op)
        => throw new NotImplementedException("runner arm: RequireWriterArg");

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
        => throw new NotImplementedException("runner arm: UnifyConstant");

    private _Step ExecUnifyStructure(RunnerContext cx, UnifyStructure op)
        => throw new NotImplementedException("runner arm: UnifyStructure");

    private _Step ExecUnifyVoid(RunnerContext cx, UnifyVoid op)
        => throw new NotImplementedException("runner arm: UnifyVoid");

    private _Step ExecUnionSiAndGoto(RunnerContext cx, UnionSiAndGoto op)
        => throw new NotImplementedException("runner arm: UnionSiAndGoto");

    // ── v2 arms ──

    private _Step ExecV2GetValue(RunnerContext cx, V2.GetValue op)
        => throw new NotImplementedException("runner arm: opv2.GetValue");

    private _Step ExecV2GetVariable(RunnerContext cx, V2.GetVariable op)
        => throw new NotImplementedException("runner arm: opv2.GetVariable");

    private _Step ExecV2HeadVariable(RunnerContext cx, V2.HeadVariable op)
        => throw new NotImplementedException("runner arm: opv2.HeadVariable");

    private _Step ExecV2PutVariable(RunnerContext cx, V2.PutVariable op)
        => throw new NotImplementedException("runner arm: opv2.PutVariable");

    private _Step ExecV2SetVariable(RunnerContext cx, V2.SetVariable op)
        => throw new NotImplementedException("runner arm: opv2.SetVariable");

    private _Step ExecV2UnifyVariable(RunnerContext cx, V2.UnifyVariable op)
        => throw new NotImplementedException("runner arm: opv2.UnifyVariable");

    private _Step ExecV2Unknown(RunnerContext cx, V2.Unknown op)
        => throw new NotImplementedException("runner arm: opv2.Unknown");

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
