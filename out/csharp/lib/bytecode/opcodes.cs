// Minimal IR for GLP bytecode runner.
// Auto-generated from lib/bytecode/opcodes.dart (feature 020 codegen drive).
// Do not edit manually — regenerate via codeconv codegen.

using LabelName = System.String;

namespace GlpRuntime.Bytecode;

/// <summary>Marker interface for all GLP bytecode opcodes.</summary>
public interface IOp { }

// ---------------------------------------------------------------------------
// Label
// ---------------------------------------------------------------------------

public class Label : IOp
{
    public LabelName Name { get; }
    public Label(LabelName name) { Name = name; }
}

// ---------------------------------------------------------------------------
// Empty marker opcodes (no fields, no ctor body)
// ---------------------------------------------------------------------------

public class ClauseTry : IOp { }
public class GuardFail : IOp { }
public class Commit : IOp { }

/// <summary>
/// clause_next: Unified instruction for clause failure/suspension.
/// Combines the behaviour of UnionSiAndGoto (when Si non-empty) and ResetAndGoto (when Si empty).
/// From spec 2.2: "discard σ̂w; jump to label of Cj"
/// </summary>
public class ClauseNext : IOp
{
    public LabelName Label { get; }
    public ClauseNext(LabelName label) { Label = label; }
}

/// <summary>
/// try_next_clause: Attempt next clause if current fails during selection phase (spec 2.4).
/// Behaviour: If current clause head fails to unify or guard fails, discard σ̂w and try next clause.
/// </summary>
public class TryNextClause : IOp { }

/// <summary>
/// no_more_clauses: All clauses exhausted without success (spec 2.5).
/// Behaviour: If suspension set non-empty, suspend goal; otherwise mark as permanently failed.
/// </summary>
public class NoMoreClauses : IOp { }

// Legacy instructions (to be replaced by ClauseNext)
[System.Obsolete]
public class UnionSiAndGoto : IOp
{
    public LabelName Label { get; }
    public UnionSiAndGoto(LabelName label) { Label = label; }
}

[System.Obsolete]
public class ResetAndGoto : IOp
{
    public LabelName Label { get; }
    public ResetAndGoto(LabelName label) { Label = label; }
}

public class SuspendEnd : IOp { }
public class Proceed : IOp { }

// ---------------------------------------------------------------------------
// Body ops (post-commit heap mutation)
// ---------------------------------------------------------------------------

public class BodySetConst : IOp
{
    public long WriterId { get; }
    public object? Value { get; }
    public BodySetConst(long writerId, object? value) { WriterId = writerId; Value = value; }
}

public class BodySetStructConstArgs : IOp
{
    public long WriterId { get; }
    public string Functor { get; }
    public List<object?> ConstArgs { get; }
    public BodySetStructConstArgs(long writerId, string functor, List<object?> constArgs)
    {
        WriterId = writerId;
        Functor = functor;
        ConstArgs = constArgs;
    }
}

/// <summary>Place constant value into argument register (BODY phase).</summary>
public class PutConstant : IOp
{
    public object? Value { get; }
    public long ArgSlot { get; }
    public PutConstant(object? value, long argSlot) { Value = value; ArgSlot = argSlot; }
}

/// <summary>
/// Create structure on heap and place reference in argument register (BODY phase).
/// WAM semantics: HEAP[H] &lt;- &lt;STR, H+1&gt;; HEAP[H+1] &lt;- F/n; Ai &lt;- HEAP[H]; H &lt;- H+2; mode &lt;- WRITE
/// </summary>
public class PutStructure : IOp
{
    public string Functor { get; }
    public long Arity { get; }
    public long ArgSlot { get; }
    public PutStructure(string functor, long arity, long argSlot)
    {
        Functor = functor;
        Arity = arity;
        ArgSlot = argSlot;
    }
}

/// <summary>
/// Build structure argument: place constant (BODY phase, WRITE mode).
/// Stores ConstTerm at HEAP[H], increments H.
/// </summary>
public class SetConstant : IOp
{
    public object? Value { get; }
    public SetConstant(object? value) { Value = value; }
}

/// <summary>
/// Place empty list [] in argument register (optimised put_constant).
/// Special case of put_constant for empty list.
/// </summary>
public class PutNil : IOp
{
    public long ArgSlot { get; }
    public PutNil(long argSlot) { ArgSlot = argSlot; }
}

/// <summary>
/// Begin list construction in argument register (optimised put_structure).
/// Equivalent to put_structure './2' or '[|]/2' depending on list functor.
/// </summary>
public class PutList : IOp
{
    public long ArgSlot { get; }
    public PutList(long argSlot) { ArgSlot = argSlot; }
}

/// <summary>
/// Put a reader pointing to a writer bound to a constant value.
/// Used for passing constants as arguments in queries.
/// </summary>
public class PutBoundConst : IOp
{
    public object? Value { get; }
    public long ArgSlot { get; }
    public PutBoundConst(object? value, long argSlot) { Value = value; ArgSlot = argSlot; }
}

/// <summary>
/// Put a reader pointing to a writer bound to 'nil'.
/// Used for passing empty lists as arguments in queries.
/// </summary>
public class PutBoundNil : IOp
{
    public long ArgSlot { get; }
    public PutBoundNil(long argSlot) { ArgSlot = argSlot; }
}

// ---------------------------------------------------------------------------
// v2.16 HEAD instructions (encode clause patterns)
// ---------------------------------------------------------------------------

/// <summary>
/// Match constant c with argument at argSlot.
/// Behaviour: Writer(w) -&gt; sigma-hat-w[w]=c; Reader(r) -&gt; Si+={r}; Ground(t) -&gt; check t==c
/// </summary>
public class HeadConstant : IOp
{
    public object? Value { get; }
    public long ArgSlot { get; }
    public HeadConstant(object? value, long argSlot) { Value = value; ArgSlot = argSlot; }
}

/// <summary>
/// Match structure f/n with argument at argSlot.
/// Sets READ/WRITE mode and S register for subsequent structure traversal.
/// </summary>
public class HeadStructure : IOp
{
    public string Functor { get; }
    public long Arity { get; }
    public long ArgSlot { get; }
    public HeadStructure(string functor, long arity, long argSlot)
    {
        Functor = functor;
        Arity = arity;
        ArgSlot = argSlot;
    }
}

/// <summary>
/// Match constant at current S position in structure.
/// Operates in READ or WRITE mode.
/// </summary>
public class UnifyConstant : IOp
{
    public object? Value { get; }
    public UnifyConstant(object? value) { Value = value; }
}

/// <summary>
/// Match empty list [] with argument (optimised head_constant).
/// Same unification semantics as head_constant with '[]' value.
/// </summary>
public class HeadNil : IOp
{
    public long ArgSlot { get; }
    public HeadNil(long argSlot) { ArgSlot = argSlot; }
}

/// <summary>
/// Match list structure [H|T] with argument (optimised head_structure).
/// Equivalent to head_structure './2' or '[|]/2' depending on list functor.
/// </summary>
public class HeadList : IOp
{
    public long ArgSlot { get; }
    public HeadList(long argSlot) { ArgSlot = argSlot; }
}

/// <summary>
/// Match void (anonymous variable) at current S position.
/// In READ mode: skip. In WRITE mode: create fresh variable.
/// </summary>
public class UnifyVoid : IOp
{
    /// <summary>Number of void positions to skip/create.</summary>
    public long Count { get; }
    public UnifyVoid(long count = 1) { Count = count; }
}

/// <summary>
/// Load argument into clause variable (first occurrence).
/// Records tentative association in sigma-hat-w during HEAD phase.
/// </summary>
public class GetVariable : IOp
{
    /// <summary>Clause variable index.</summary>
    public long VarIndex { get; }
    /// <summary>Argument register.</summary>
    public long ArgSlot { get; }
    public GetVariable(long varIndex, long argSlot) { VarIndex = varIndex; ArgSlot = argSlot; }
}

/// <summary>
/// Unify argument with clause variable (subsequent occurrence).
/// Performs writer MGU, updates sigma-hat-w during HEAD phase.
/// </summary>
public class GetValue : IOp
{
    /// <summary>Clause variable index.</summary>
    public long VarIndex { get; }
    /// <summary>Argument register.</summary>
    public long ArgSlot { get; }
    public GetValue(long varIndex, long argSlot) { VarIndex = varIndex; ArgSlot = argSlot; }
}

// ---------------------------------------------------------------------------
// GUARD instructions (pure tests during HEAD/GUARDS phase)
// ---------------------------------------------------------------------------

/// <summary>
/// Otherwise guard: succeeds if all previous clauses failed (not suspended).
/// Checks if Si is empty when executed — if so, all previous clauses definitely failed.
/// If Si is non-empty, previous clauses suspended, so this fails.
/// </summary>
public class Otherwise : IOp { }

/// <summary>
/// Push: Save current structure processing state before entering nested structure.
/// Stores (S, mode, currentStructure) triple in clause variable Xi.
/// Following FCP AM design for nested structure handling.
/// </summary>
public class Push : IOp
{
    /// <summary>Xi register to store state.</summary>
    public long RegIndex { get; }
    public Push(long regIndex) { RegIndex = regIndex; }

    public override string ToString() => $"Push(X{RegIndex})";
}

/// <summary>
/// Pop: Restore structure processing state after completing nested structure.
/// Retrieves (S, mode, currentStructure) from clause variable Xi.
/// Must correspond to a previous Push instruction.
/// </summary>
public class Pop : IOp
{
    /// <summary>Xi register to restore from.</summary>
    public long RegIndex { get; }
    public Pop(long regIndex) { RegIndex = regIndex; }

    public override string ToString() => $"Pop(X{RegIndex})";
}

/// <summary>
/// UnifyStructure: Process nested structure at current S position.
/// Following FCP AM's unify_compound instruction.
/// Matches/creates structure at args[S], then enters that structure for processing.
/// </summary>
public class UnifyStructure : IOp
{
    public string Functor { get; }
    public long Arity { get; }

    public UnifyStructure(string functor, long arity) { Functor = functor; Arity = arity; }

    public override string ToString() => $"UnifyStructure({Functor}, {Arity})";
}

/// <summary>
/// Guard predicate call: execute guard without side effects.
/// If succeeds: continue. If fails: try next clause. If suspends: suspend entire goal.
/// If negated: invert success/fail result (suspend unchanged).
/// </summary>
public class Guard : IOp
{
    /// <summary>Guard predicate entry.</summary>
    public LabelName ProcedureLabel { get; }
    /// <summary>Number of arguments.</summary>
    public long Arity { get; }
    /// <summary>True if ~G (guard negation).</summary>
    public bool Negated { get; }
    public Guard(LabelName procedureLabel, long arity, bool negated = false)
    {
        ProcedureLabel = procedureLabel;
        Arity = arity;
        Negated = negated;
    }
}

/// <summary>
/// Ground test: test if variable contains no unbound variables.
/// Succeed if X is ground, fail otherwise. Pure test, no side effects.
/// If negated: succeed if X is NOT ground (contains unbound variables).
/// </summary>
public class Ground : IOp
{
    /// <summary>Clause variable index to test.</summary>
    public long VarIndex { get; }
    /// <summary>True if ~ground(X).</summary>
    public bool Negated { get; }
    public Ground(long varIndex, bool negated = false) { VarIndex = varIndex; Negated = negated; }
}

/// <summary>
/// Known test: test if variable is not an unbound variable.
/// Succeed if X is not a variable, fail otherwise. Pure test operation.
/// If negated: succeed if X IS an unbound variable.
/// </summary>
public class Known : IOp
{
    /// <summary>Clause variable index to test.</summary>
    public long VarIndex { get; }
    /// <summary>True if ~known(X).</summary>
    public bool Negated { get; }
    public Known(long varIndex, bool negated = false) { VarIndex = varIndex; Negated = negated; }
}

/// <summary>
/// NoReaders test: test if term contains no readers.
/// Three-valued semantics:
/// SUCCESS: Term contains no readers (ground terms and/or writers only).
/// SUSPEND: Term contains readers (even bound ones need to be traversed).
/// FAILURE: Never fails (per spec).
/// If negated: ~no_readers(X) succeeds if X contains readers.
/// </summary>
public class NoReaders : IOp
{
    /// <summary>Clause variable index to test.</summary>
    public long VarIndex { get; }
    /// <summary>True if ~no_readers(X).</summary>
    public bool Negated { get; }
    public NoReaders(long varIndex, bool negated = false) { VarIndex = varIndex; Negated = negated; }
}

/// <summary>
/// Ground equality test: X =?= Y.
/// Tests if two terms are structurally equal when both are ground.
/// Three-valued semantics:
/// SUCCESS: Both terms ground and structurally equal.
/// SUSPEND: Either term contains unbound readers (add to Si).
/// FAILURE: Both terms ground but not equal.
/// Left-to-right evaluation order: checks X first, then Y.
/// If negated: inverts success/failure (suspend unchanged).
/// </summary>
public class GroundEqual : IOp
{
    /// <summary>Clause variable index for left operand.</summary>
    public long LeftVarIndex { get; }
    /// <summary>Clause variable index for right operand.</summary>
    public long RightVarIndex { get; }
    /// <summary>True if ~(X =?= Y).</summary>
    public bool Negated { get; }
    public GroundEqual(long leftVarIndex, long rightVarIndex, bool negated = false)
    {
        LeftVarIndex = leftVarIndex;
        RightVarIndex = rightVarIndex;
        Negated = negated;
    }

    public override string ToString() => Negated
        ? $"~(X{LeftVarIndex} =?= X{RightVarIndex})"
        : $"X{LeftVarIndex} =?= X{RightVarIndex}";
}

// ---------------------------------------------------------------------------
// Legacy opcodes (for backward compatibility with existing tests)
// ---------------------------------------------------------------------------

public class HeadBindWriter : IOp
{
    public long WriterId { get; }
    public HeadBindWriter(long writerId) { WriterId = writerId; }
}

public class GuardNeedReader : IOp
{
    public long ReaderId { get; }
    public GuardNeedReader(long readerId) { ReaderId = readerId; }
}

// ---------------------------------------------------------------------------
// Argument-slot variants (program fixed; ids supplied at runtime)
// ---------------------------------------------------------------------------

public class RequireWriterArg : IOp
{
    /// <summary>Argument index (0 for p/1).</summary>
    public long Slot { get; }
    /// <summary>Jump if not a writer call.</summary>
    public LabelName FailLabel { get; }
    public RequireWriterArg(long slot, LabelName failLabel) { Slot = slot; FailLabel = failLabel; }
}

public class RequireReaderArg : IOp
{
    /// <summary>Argument index (0 for p/1).</summary>
    public long Slot { get; }
    /// <summary>Jump if not a reader call.</summary>
    public LabelName FailLabel { get; }
    public RequireReaderArg(long slot, LabelName failLabel) { Slot = slot; FailLabel = failLabel; }
}

public class HeadBindWriterArg : IOp
{
    /// <summary>Add writer(slot) to sigma-hat-w.</summary>
    public long Slot { get; }
    public HeadBindWriterArg(long slot) { Slot = slot; }
}

public class GuardNeedReaderArg : IOp
{
    /// <summary>Add reader(slot) to Si iff unbound.</summary>
    public long Slot { get; }
    public GuardNeedReaderArg(long slot) { Slot = slot; }
}

public class BodySetConstArg : IOp
{
    /// <summary>Bind writer(slot) := value (post-commit only).</summary>
    public long Slot { get; }
    public object? Value { get; }
    public BodySetConstArg(long slot, object? value) { Slot = slot; Value = value; }
}

// ---------------------------------------------------------------------------
// Scheduler / fairness
// ---------------------------------------------------------------------------

public class TailStep : IOp
{
    public LabelName Label { get; }
    public TailStep(LabelName label) { Label = label; }
}

/// <summary>
/// Spawn new goal for procedure P with arguments in A1-An.
/// Non-tail call: saves continuation and schedules new goal.
/// </summary>
public class Spawn : IOp
{
    /// <summary>Procedure entry label.</summary>
    public LabelName ProcedureLabel { get; }
    /// <summary>Number of arguments.</summary>
    public long Arity { get; }
    public Spawn(LabelName procedureLabel, long arity) { ProcedureLabel = procedureLabel; Arity = arity; }
}

/// <summary>
/// Tail call to procedure P with arguments in A1-An.
/// Reuses current goal frame, implements fair scheduling via tail recursion budget.
/// </summary>
public class Requeue : IOp
{
    /// <summary>Procedure entry label.</summary>
    public LabelName ProcedureLabel { get; }
    /// <summary>Number of arguments.</summary>
    public long Arity { get; }
    public Requeue(LabelName procedureLabel, long arity) { ProcedureLabel = procedureLabel; Arity = arity; }
}

/// <summary>
/// Create environment frame with n permanent variables.
/// Push new frame on local stack, save E and CP in frame, update E to point to new frame.
/// </summary>
public class Allocate : IOp
{
    /// <summary>Number of permanent variable slots (Y1-Yn).</summary>
    public long Slots { get; }
    public Allocate(long slots) { Slots = slots; }
}

/// <summary>
/// Remove current environment frame.
/// Restore previous E and CP from frame, pop frame from stack.
/// </summary>
public class Deallocate : IOp { }

/// <summary>
/// No operation — advance PC without other effects.
/// Used for alignment or patching.
/// </summary>
public class Nop : IOp { }

/// <summary>Terminate execution — mark goal as completed, return control to scheduler.</summary>
public class Halt : IOp { }

// ---------------------------------------------------------------------------
// Module System Opcodes (Phase 2)
// ---------------------------------------------------------------------------

/// <summary>
/// Distribute: Static RPC to imported module at known index.
/// Following FCP: distribute # {Index, Goal}.
/// Writes message to import vector at Index, which routes to target module.
/// Index is 1-based (FCP convention).
/// </summary>
public class Distribute : IOp
{
    /// <summary>Index in import vector (1-based).</summary>
    public long ImportIndex { get; }
    /// <summary>Goal functor.</summary>
    public string Functor { get; }
    /// <summary>Goal arity.</summary>
    public long Arity { get; }

    public Distribute(long importIndex, string functor, long arity)
    {
        ImportIndex = importIndex;
        Functor = functor;
        Arity = arity;
    }

    public override string ToString() => $"Distribute([{ImportIndex}] {Functor}/{Arity})";
}

/// <summary>
/// Transmit: Dynamic RPC to module resolved at runtime.
/// Following FCP: transmit # {ModuleVar, Goal}.
/// Resolves module name from variable, looks up in registry, sends message.
/// Used when target module is not known at compile time.
/// </summary>
public class Transmit : IOp
{
    /// <summary>Register holding module name variable.</summary>
    public long ModuleVarIndex { get; }
    /// <summary>Goal functor.</summary>
    public string Functor { get; }
    /// <summary>Goal arity.</summary>
    public long Arity { get; }

    public Transmit(long moduleVarIndex, string functor, long arity)
    {
        ModuleVarIndex = moduleVarIndex;
        Functor = functor;
        Arity = arity;
    }

    public override string ToString() => $"Transmit(X{ModuleVarIndex}, {Functor}/{Arity})";
}
