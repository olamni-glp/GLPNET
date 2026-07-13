//// glp/engine/runner — the three-phase bytecode runner (feature 050, T021).
////
//// Executes one goal reduction: run the goal's clauses through HEAD (tentative
//// unification into σ̂w) → GUARD (pure tests) → BODY (commit σ̂w to the heap,
//// construct body goals). Writer-MGU discipline: HEAD binds only writers, into
//// σ̂w, applied to the heap atomically at Commit; readers are never bound
//// (PI:14, contracts/proof-obligations.md).
////
//// Dart source of truth: glp_runtime/lib/bytecode/runner.dart
//// (`BytecodeRunner.runWithStatus` / `RunnerContext`). The Dart runner is a
//// mutable `if (op is X)` dispatch chain over `pc`; this port is an immutable
//// recursive stepper over the same instruction stream and the same σ̂w / Si / U
//// state, threading the immutable `glp/runtime/heap` value.
////
//// ── Port status (T021 is sliced; frozen semantics — no guessing, gaps STOP) ──
//// Slices 21a/b DONE: control spine + HEAD-constant + Commit + suspension.
//// Slice 21c (THIS) adds the HEAD-STRUCTURE machinery — the writer-MGU crux:
//// `HeadStructure`, the unified `GetVariable`/`GetValue` (Dart opv2 handlers),
//// `UnifyVariable`/`UnifyConstant`/`UnifyVoid`, and nested-structure `Push`/
//// `Pop`/`UnifyStructure`, with the tentative-structure (`_TentativeStruct`) and
//// clause-variable (`clauseVars`, `_ClauseVar`) state. BODY put_*/spawn +
//// parent-stack completion (21d), GUARD eval (T023), kernels (T024) are the
//// remaining slices; any not-yet-ported opcode returns
//// `RunnerError(Unimplemented(..))` — surfaced, never silently skipped.
////
//// Suspension model adaptation (faithful, documented): the Dart keeps reader
//// addresses in Si/U and maps to paired writers at suspend time; the Gleam
//// foundation reactivates on WRITER binding (heap.suspend_on_writer /
//// bind_writer), and `deref` of an unbound reader already yields its terminal
//// writer — so Si/U and `Suspended(on:)` carry WRITER addresses, which the
//// scheduler (T022) registers directly against the suspension table.
////
//// Tentative-structure sync adaptation: the Dart shares one mutable
//// `_TentativeStruct` reference between `sigmaHat[wid]` and `currentStructure`,
//// so incremental fills are visible in both. This immutable port keeps the
//// active structure in `current` and re-writes `sigma_hat[current_writer]` on
//// every fill (see `put_current`), reproducing the shared-reference behaviour.

import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/order
import gleam/result
import gleam/set.{type Set}
import gleam/string
import glp/bytecode/opcodes.{type LabelName, type Op}
import glp/bytecode/program.{type BytecodeProgram, type XRegs}
import glp/engine/arith.{type NumV, NInt, NReal}
import glp/engine/kernels
import glp/runtime/heap.{type Heap, type HeapError, Bound, Unbound}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{
  type Constant, type Term, ConstAtom, ConstInt, ConstReal, ConstString,
  ConstTerm, StructTerm, VarRef,
}

/// A body-spawned goal: a request for the scheduler (T022) to mint a fresh goal
/// id, register `regs`, and enqueue an `Activation` at `entry_pc` for procedure
/// `procedure`. The runner deliberately does NOT own the goal-id counter (Dart
/// `rt.nextGoalId`); it emits the request and the scheduler assigns identity.
pub type SpawnReq {
  SpawnReq(procedure: LabelName, entry_pc: Int, regs: XRegs)
}

/// The outcome of one goal reduction, handed back to the scheduler (T022).
pub type ReduceOutcome {
  /// A clause committed: the goal reduced. `heap` is post-commit; `woken`
  /// carries reactivation signals (goal id + resume pc) from writers the commit
  /// bound; `spawned` carries the body's freshly-built goals.
  Reduced(
    heap: Heap,
    woken: List(GoalRef),
    spawned: List(SpawnReq),
    output: List(String),
  )
  /// All clauses were exhausted with a non-empty goal-level suspension set: the
  /// goal suspends on the writer addresses in `on` (reactivate when any binds).
  Suspended(heap: Heap, on: Set(Int))
  /// All clauses were exhausted with nothing to wait on: permanent failure.
  Failed(heap: Heap)
  /// The reduction budget was exhausted mid-run (fairness / loop backstop).
  BudgetExhausted(heap: Heap)
  /// A structural violation or a not-yet-ported opcode — surfaced, never hidden.
  RunnerError(reason: RunnerFault)
}

/// Why a reduction could not proceed (kept distinct from an ordinary Fail).
pub type RunnerFault {
  /// An opcode this slice does not yet handle (names the mnemonic; the port is
  /// sliced — see the module header).
  Unimplemented(mnemonic: String)
  /// A writer↔writer binding or double-bind detected while applying σ̂w.
  StructuralViolation(detail: String)
  /// A malformed program/goal (missing register, PC past the stream end).
  Malformed(detail: String)
}

/// READ (matching an incoming structure) vs WRITE (building an output
/// structure). Dart `UnifyMode`.
pub type Mode {
  ReadMode
  WriteMode
}

/// A σ̂w value: a resolved term, or a HEAD-phase tentative structure (converted
/// to a real `StructTerm` at Commit). Dart `sigmaHat` is `Map<int,Object?>`
/// holding `ConstTerm`/`StructTerm`/`VarRef` or a `_TentativeStruct`.
pub type SigmaVal {
  SVTerm(Term)
  SVTentative(TentStruct)
}

/// A HEAD-phase structure under construction (Dart `_TentativeStruct`): filled
/// slot-by-slot in WRITE mode, converted to a `StructTerm` at Commit.
pub type TentStruct {
  TentStruct(functor: String, arity: Int, args: List(TentSlot))
}

/// One slot of a tentative structure (Dart `Object?` in `_TentativeStruct.args`).
pub type TentSlot {
  /// A resolved term (VarRef/ConstTerm/StructTerm).
  TSTerm(Term)
  /// A nested tentative structure.
  TSNested(TentStruct)
  /// Dart `null` — a void / anonymous slot (UnifyVoid WRITE mode).
  TSVoid
  /// Dart `_ClauseVar` placeholder — resolved at Commit (rare fallback).
  TSClauseVar(var_index: Int, is_writer: Bool)
}

/// A clause-variable binding — Dart `cx.clauseVars[i]`, an `Object?`. The bare
/// `int` (a raw addr whose role the context interprets) is kept distinct from a
/// `VarRef` exactly as the reference distinguishes them.
pub type CVar {
  CVAddr(Int)
  CVTerm(Term)
  CVTentative(TentStruct)
  CVState(
    saved_s: Int,
    saved_mode: Mode,
    saved_target: BuildTarget,
    saved_writer: Option(Int),
  )
}

/// The structure currently being traversed (READ) or built (WRITE); Dart
/// `cx.currentStructure`. `BTStruct` doubles as a real `StructTerm` under READ
/// traversal (HEAD) and a BODY structure being filled slot-by-slot.
pub type BuildTarget {
  BTNone
  BTStruct(functor: String, args: List(Term))
  /// WRITE-mode HEAD building.
  BTTentative(TentStruct)
}

/// A saved parent context for nested BODY structure building (Dart
/// `_ParentContext`): the parent structure, its S pointer, its mode, and its
/// target writer.
pub type ParentCtx {
  ParentCtx(structure: BuildTarget, s: Int, mode: Mode, writer: Option(Int))
}

/// Per-reduction execution context (Dart `RunnerContext`, the fields this slice
/// uses). Immutable — every handler returns a new context.
pub type RunnerContext {
  RunnerContext(
    heap: Heap,
    /// This goal's argument registers A0..An-1 (call args as terms).
    regs: XRegs,
    /// σ̂w — tentative writer bindings (writer addr → value) built in HEAD,
    /// applied to the heap atomically at Commit.
    sigma_hat: Dict(Int, SigmaVal),
    /// Per-clause preliminary suspension set (writer addresses).
    si: Set(Int),
    /// Goal-level suspension set (writer addresses), accumulated across clause
    /// attempts; consumed by NoMoreClauses. Survives clause clears.
    u: Set(Int),
    /// Reactivation signals from writers bound at Commit / in BODY (Dart
    /// `rt.gq.enqueue` of woken `GoalRef`s).
    woken: List(GoalRef),
    /// Body-spawned goal requests (Dart Spawn), handed to the scheduler.
    spawn_reqs: List(SpawnReq),
    /// Clause-variable bindings Xi → value (Dart `clauseVars`).
    clause_vars: Dict(Int, CVar),
    /// READ/WRITE mode for structure traversal.
    mode: Mode,
    /// Structure pointer — index into the current structure's args.
    s: Int,
    /// The structure being traversed/built.
    current: BuildTarget,
    /// σ̂w key of the top-level tentative in `current` (see `put_current`);
    /// `None` while a nested structure is being built or during READ traversal.
    current_writer: Option(Int),
    /// BODY output argument registers for the next Spawn (Dart `argSlots`).
    arg_slots: Dict(Int, Term),
    /// The target writer of the BODY structure under construction (Dart
    /// `clauseVars[-1]`).
    build_writer: Option(Int),
    /// The target arg slot of the outermost BODY structure (Dart
    /// `clauseVars[-2]`).
    build_slot: Option(Int),
    /// Saved parent contexts for nested BODY structure building (Dart
    /// `parentStack`).
    parent_stack: List(ParentCtx),
    /// Phase flag: `False` = HEAD/GUARD, `True` = BODY (set at Commit).
    in_body: Bool,
    /// Captured `_output/1` program-output lines accumulated during this reduction
    /// (T034), handed to the scheduler on `Reduced` (never touches the heap).
    output: List(String),
  )
}

/// A fresh reduction context for a goal with argument registers `regs` over
/// `heap` (the scheduler / engine facade builds this; exposed as the T021 unit
/// seam).
pub fn new_context(heap: Heap, regs: XRegs) -> RunnerContext {
  RunnerContext(
    heap: heap,
    regs: regs,
    sigma_hat: dict.new(),
    si: set.new(),
    u: set.new(),
    woken: [],
    spawn_reqs: [],
    clause_vars: dict.new(),
    mode: ReadMode,
    s: 0,
    current: BTNone,
    current_writer: None,
    arg_slots: dict.new(),
    build_writer: None,
    build_slot: None,
    parent_stack: [],
    in_body: False,
    output: [],
  )
}

/// Run one reduction of the goal whose procedure entry PC is `kappa`, over
/// `program`, starting from context `ctx`. `budget` bounds the number of
/// instructions executed (fairness / non-termination backstop).
pub fn reduce(
  program: BytecodeProgram,
  ctx: RunnerContext,
  kappa: Int,
  budget: Int,
) -> ReduceOutcome {
  run_loop(program, ctx, kappa, budget)
}

// ── The dispatch loop ───────────────────────────────────────────────────────

/// The result of stepping one instruction.
type Step {
  /// Continue at pc+1 with the updated context.
  Advance(RunnerContext)
  /// Continue at an explicit pc (clause jump / soft-fail target).
  Jump(RunnerContext, Int)
  /// Terminate the reduction with this outcome.
  Stop(ReduceOutcome)
}

fn run_loop(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  budget: Int,
) -> ReduceOutcome {
  case budget <= 0 {
    True -> BudgetExhausted(ctx.heap)
    False ->
      case program.op_at(program, pc) {
        Error(_) ->
          RunnerError(Malformed(
            "pc " <> int.to_string(pc) <> " past end of instruction stream",
          ))
        Ok(op) ->
          case step(program, ctx, op, pc) {
            Advance(ctx) -> run_loop(program, ctx, pc + 1, budget - 1)
            Jump(ctx, target) -> run_loop(program, ctx, target, budget - 1)
            Stop(outcome) -> outcome
          }
      }
  }
}

fn step(program: BytecodeProgram, ctx: RunnerContext, op: Op, pc: Int) -> Step {
  case op {
    // ── Control / clause selection ──────────────────────────────────────────
    opcodes.Label(_) -> Advance(ctx)
    opcodes.Nop -> Advance(ctx)
    opcodes.ClauseTry -> Advance(clear_clause(ctx))
    opcodes.ClauseNext(label) -> {
      let ctx = clear_clause(RunnerContext(..ctx, u: set.union(ctx.u, ctx.si)))
      case program.label_pc(program, label) {
        Ok(target) -> Jump(ctx, target)
        Error(_) ->
          Stop(RunnerError(Malformed("clause_next: unknown label " <> label)))
      }
    }
    opcodes.TryNextClause -> soft_fail(program, ctx, pc)
    opcodes.NoMoreClauses -> Stop(no_more_clauses(ctx))
    opcodes.Commit -> commit(program, ctx, pc)
    opcodes.Proceed ->
      Stop(Reduced(ctx.heap, ctx.woken, ctx.spawn_reqs, ctx.output))
    opcodes.Halt ->
      Stop(Reduced(ctx.heap, ctx.woken, ctx.spawn_reqs, ctx.output))

    // ── HEAD phase: constants ───────────────────────────────────────────────
    opcodes.HeadConstant(value, arg_slot) ->
      head_match_constant(program, ctx, pc, value, arg_slot)
    opcodes.HeadNil(arg_slot) ->
      head_match_constant(program, ctx, pc, ConstAtom("nil"), arg_slot)

    // ── HEAD phase: argument load / unify (Dart opv2.GetVariable/GetValue) ──
    opcodes.GetVariable(var_index, arg_slot, is_reader) ->
      get_variable(program, ctx, pc, var_index, arg_slot, is_reader)
    opcodes.GetValue(var_index, arg_slot, is_reader) ->
      get_value(program, ctx, pc, var_index, arg_slot, is_reader)

    // ── HEAD phase: structures ──────────────────────────────────────────────
    opcodes.HeadStructure(functor, arity, arg_slot) ->
      head_structure(program, ctx, pc, functor, arity, arg_slot)
    opcodes.UnifyVariable(var_index, is_reader) ->
      unify_variable(program, ctx, pc, var_index, is_reader)
    opcodes.UnifyConstant(value) -> unify_constant(program, ctx, pc, value)
    opcodes.UnifyVoid(count) -> unify_void(ctx, count)
    opcodes.Push(reg_index) -> push(ctx, reg_index)
    opcodes.Pop(reg_index) -> pop(ctx, reg_index)
    opcodes.UnifyStructure(functor, arity) ->
      unify_structure(program, ctx, pc, functor, arity)

    // ── BODY phase: argument construction + spawn (slice 21d) ───────────────
    opcodes.PutVariable(var_index, arg_slot, is_reader) ->
      put_variable(ctx, var_index, arg_slot, is_reader)
    opcodes.PutConstant(value, arg_slot)
    | opcodes.PutBoundConst(value, arg_slot) ->
      put_bound_const(ctx, value, arg_slot)
    opcodes.PutNil(arg_slot) | opcodes.PutBoundNil(arg_slot) ->
      put_bound_const(ctx, ConstAtom("nil"), arg_slot)
    opcodes.PutStructure(functor, arity, arg_slot) ->
      put_structure(ctx, functor, arity, arg_slot)
    opcodes.SetVariable(var_index, is_reader) ->
      body_element_var(ctx, var_index, is_reader)
    opcodes.SetConstant(value) -> body_element_const(ctx, value)
    opcodes.Spawn(label, arity) -> spawn(program, ctx, label, arity)

    // ── GUARD phase: pure three-valued tests (slice 21e / T023) ─────────────
    opcodes.Otherwise ->
      case set.is_empty(ctx.u) {
        True -> Advance(ctx)
        False -> soft_fail(program, ctx, pc)
      }
    opcodes.Ground(var_index, negated) ->
      guard_ground(program, ctx, pc, var_index, negated)
    opcodes.Known(var_index, negated) ->
      guard_known(program, ctx, pc, var_index, negated)
    opcodes.Unknown(var_index) -> guard_unknown(program, ctx, pc, var_index)
    opcodes.NoReaders(var_index, negated) ->
      guard_no_readers(program, ctx, pc, var_index, negated)
    opcodes.GroundEqual(left, right, negated) ->
      guard_ground_equal(program, ctx, pc, left, right, negated)

    // ── GUARD phase: the generic Guard opcode (T024) ────────────────────────
    opcodes.Guard(predicate, arity, negated) ->
      guard_generic(program, ctx, pc, predicate, arity, negated)

    // ── Not yet ported (surfaced, never silently skipped) ───────────────────
    _ -> Stop(RunnerError(Unimplemented(opcodes.mnemonic(op))))
  }
}

// ── Read-only heap helpers (deref ignoring path-compression, FR-009) ─────────
//
// Path compression is an internal layout optimization excluded from parity, so
// these read-only probes drop the compressed heap `deref` returns.

fn dval(heap: Heap, addr: Int) -> heap.DerefResult {
  case heap.deref(heap, addr) {
    Ok(#(_, result)) -> result
    // A cycle/WxW during a HEAD read is a structural anomaly; the corpus never
    // produces one. Treat as unbound-self so the caller's suspend/fail path (not
    // a crash) handles it; a genuine violation still surfaces at Commit.
    Error(_) -> heap.Unbound(addr)
  }
}

/// Is the writer at `addr` already bound on the HEAP (not σ̂w)? Mirrors the
/// Dart `isWriterBound` — a writer bound to a value, or onward past itself.
fn heap_bound(heap: Heap, addr: Int) -> Bool {
  case dval(heap, addr) {
    Bound(_) -> True
    Unbound(terminal) -> terminal != addr
  }
}

// ── HEAD constant matching (Dart HeadConstant, runner.dart:1126) ─────────────

fn head_match_constant(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  value: Constant,
  arg_slot: Int,
) -> Step {
  case program.get_reg(ctx.regs, arg_slot) {
    Error(_) ->
      Stop(
        RunnerError(Malformed(
          "head_constant: no argument register A" <> int.to_string(arg_slot),
        )),
      )
    Ok(term) ->
      case resolve_arg(ctx, term) {
        ArgErr(fault) -> Stop(RunnerError(fault))
        ArgValue(ConstTerm(c)) ->
          case c == value {
            True -> Advance(ctx)
            False -> soft_fail(program, ctx, pc)
          }
        ArgValue(_) -> soft_fail(program, ctx, pc)
        ArgWriter(writer) ->
          case dict.get(ctx.sigma_hat, writer) {
            Ok(SVTerm(ConstTerm(c))) ->
              case c == value {
                True -> Advance(ctx)
                False -> soft_fail(program, ctx, pc)
              }
            Ok(_) -> soft_fail(program, ctx, pc)
            Error(_) ->
              Advance(
                RunnerContext(
                  ..ctx,
                  sigma_hat: dict.insert(
                    ctx.sigma_hat,
                    writer,
                    SVTerm(ConstTerm(value)),
                  ),
                ),
              )
          }
        ArgReader(paired_writer) ->
          Advance(RunnerContext(..ctx, si: set.insert(ctx.si, paired_writer)))
      }
  }
}

/// The resolved role of an argument term (writer/reader carry the terminal
/// writer address).
type ArgRole {
  ArgValue(Term)
  ArgWriter(Int)
  ArgReader(Int)
  ArgErr(RunnerFault)
}

fn resolve_arg(ctx: RunnerContext, term: Term) -> ArgRole {
  case term {
    VarRef(addr) ->
      case heap.deref(ctx.heap, addr) {
        Error(e) -> ArgErr(StructuralViolation(heap_error_detail(e)))
        Ok(#(_, Bound(value))) -> ArgValue(value)
        Ok(#(_, Unbound(writer))) ->
          case heap.is_writer(ctx.heap, addr) {
            True -> ArgWriter(writer)
            False -> ArgReader(writer)
          }
      }
    _ -> ArgValue(term)
  }
}

// ── GetVariable (Dart opv2.GetVariable, runner.dart:2201) ────────────────────
//
// First-occurrence load of argument Ai into clause variable Xi under the
// writer/reader-mode discipline. Also serves the struct-extraction case (Dart
// v1 `GetVariable(temp, argSlot)`), which the Gleam codegen emits as the same
// unified opcode with `is_reader = False`.

fn get_variable(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  var_index: Int,
  arg_slot: Int,
  is_reader: Bool,
) -> Step {
  case program.get_reg(ctx.regs, arg_slot) {
    Error(_) -> soft_fail(program, ctx, pc)
    Ok(arg) -> {
      let existing = dict.get(ctx.clause_vars, var_index)
      case is_reader {
        False -> get_variable_writer(ctx, var_index, arg, existing)
        True -> get_variable_reader(program, ctx, pc, var_index, arg, existing)
      }
    }
  }
}

/// The writer address an existing clause-var denotes when interpreted as a
/// writer (Dart `existing is VarRef&&isWriter` / `existing is int`).
fn existing_writer(
  ctx: RunnerContext,
  existing: Result(CVar, Nil),
) -> Result(Int, Nil) {
  case existing {
    Ok(CVTerm(VarRef(e))) ->
      case heap.is_writer(ctx.heap, e) {
        True -> Ok(e)
        False -> Error(Nil)
      }
    Ok(CVAddr(e)) -> Ok(e)
    _ -> Error(Nil)
  }
}

fn get_variable_writer(
  ctx: RunnerContext,
  var_index: Int,
  arg: Term,
  existing: Result(CVar, Nil),
) -> Step {
  case arg {
    VarRef(addr) ->
      case heap.is_writer(ctx.heap, addr), heap.is_reader(ctx.heap, addr) {
        // Goal writer vs head writer.
        True, _ ->
          case existing_writer(ctx, existing) {
            // Already have a writer: bind arg writer onward to its reader.
            Ok(e) ->
              Advance(bind_sigma(
                ctx,
                addr,
                VarRef(heap.paired_reader(ctx.heap, e)),
              ))
            Error(_) ->
              case dval(ctx.heap, addr) {
                Bound(v) -> Advance(set_cvar(ctx, var_index, CVTerm(v)))
                Unbound(_) ->
                  Advance(set_cvar(ctx, var_index, CVTerm(VarRef(addr))))
              }
          }
        // Goal reader.
        _, True ->
          case dval(ctx.heap, addr) {
            Bound(v) ->
              case existing_writer(ctx, existing) {
                Ok(e) -> Advance(bind_sigma(ctx, e, v))
                Error(_) -> Advance(set_cvar(ctx, var_index, CVTerm(v)))
              }
            Unbound(_) ->
              case existing_writer(ctx, existing) {
                Ok(e) -> Advance(bind_sigma(ctx, e, VarRef(addr)))
                Error(_) ->
                  Advance(set_cvar(ctx, var_index, CVTerm(VarRef(addr))))
              }
          }
        _, _ -> Advance(ctx)
      }
    // Ground term.
    _ ->
      case existing_writer(ctx, existing) {
        Ok(e) -> Advance(bind_sigma(ctx, e, arg))
        Error(_) -> Advance(set_cvar(ctx, var_index, CVTerm(arg)))
      }
  }
}

fn get_variable_reader(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  var_index: Int,
  arg: Term,
  existing: Result(CVar, Nil),
) -> Step {
  case arg {
    VarRef(addr) ->
      case heap.is_writer(ctx.heap, addr), heap.is_reader(ctx.heap, addr) {
        // Goal writer → head reader: clause observes the goal's variable.
        True, _ ->
          case existing {
            Ok(_) ->
              case existing_writer(ctx, existing) {
                Ok(e) ->
                  Advance(bind_sigma(
                    ctx,
                    addr,
                    VarRef(heap.paired_reader(ctx.heap, e)),
                  ))
                Error(_) ->
                  case existing {
                    Ok(CVTerm(t)) -> Advance(bind_sigma(ctx, addr, t))
                    _ -> Advance(ctx)
                  }
              }
            // First occurrence: store the goal writer addr (Dart stores `int`).
            Error(_) -> Advance(set_cvar(ctx, var_index, CVAddr(addr)))
          }
        // Reader × Reader = FAIL (a writers-only substitution can't equate two
        // readers — spec §12.2 Case 2 / CGLP Def 5).
        _, True -> soft_fail(program, ctx, pc)
        _, _ -> store_if_absent(ctx, var_index, arg, existing)
      }
    _ -> store_if_absent(ctx, var_index, arg, existing)
  }
}

fn store_if_absent(
  ctx: RunnerContext,
  var_index: Int,
  arg: Term,
  existing: Result(CVar, Nil),
) -> Step {
  case existing {
    Error(_) -> Advance(set_cvar(ctx, var_index, CVTerm(arg)))
    Ok(_) -> Advance(ctx)
  }
}

// ── GetValue (Dart opv2.GetValue, runner.dart:2362) ──────────────────────────
//
// Subsequent-occurrence unification of argument Ai with clause variable Xi.

fn get_value(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  var_index: Int,
  arg_slot: Int,
  is_reader: Bool,
) -> Step {
  case program.get_reg(ctx.regs, arg_slot) {
    Error(_) -> soft_fail(program, ctx, pc)
    Ok(arg) ->
      case dict.get(ctx.clause_vars, var_index) {
        Error(_) -> soft_fail(program, ctx, pc)
        Ok(stored) ->
          case is_reader {
            False -> get_value_writer(program, ctx, pc, arg, stored)
            True -> get_value_reader(program, ctx, pc, arg, stored)
          }
      }
  }
}

fn get_value_writer(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  arg: Term,
  stored: CVar,
) -> Step {
  case arg {
    VarRef(addr) ->
      case heap.is_writer(ctx.heap, addr), heap.is_reader(ctx.heap, addr) {
        True, _ ->
          case dval(ctx.heap, addr) {
            Bound(arg_value) ->
              case stored {
                CVAddr(s) ->
                  case dval(ctx.heap, s) {
                    Bound(stored_val) ->
                      case terms_shape_equal(arg_value, stored_val) {
                        True -> Advance(ctx)
                        False -> soft_fail(program, ctx, pc)
                      }
                    Unbound(_) -> Advance(bind_sigma(ctx, s, arg_value))
                  }
                CVTerm(sv) ->
                  case terms_shape_equal(arg_value, sv) {
                    True -> Advance(ctx)
                    False -> soft_fail(program, ctx, pc)
                  }
                _ -> soft_fail(program, ctx, pc)
              }
            Unbound(_) ->
              case stored {
                CVAddr(s) ->
                  case dict.get(ctx.sigma_hat, s) {
                    Ok(fresh) -> Advance(bind_sigma_val(ctx, addr, fresh))
                    Error(_) ->
                      case addr == s {
                        True -> Advance(ctx)
                        False -> soft_fail(program, ctx, pc)
                      }
                  }
                CVTerm(sv) -> Advance(bind_sigma(ctx, addr, sv))
                _ -> Advance(ctx)
              }
          }
        _, True ->
          case dval(ctx.heap, addr) {
            Bound(reader_value) ->
              case stored {
                CVAddr(s) -> Advance(bind_sigma(ctx, s, reader_value))
                CVTerm(sv) ->
                  case sv == reader_value {
                    True -> Advance(ctx)
                    False -> soft_fail(program, ctx, pc)
                  }
                _ -> soft_fail(program, ctx, pc)
              }
            Unbound(_) ->
              case stored {
                CVAddr(s) ->
                  case heap.paired_writer(ctx.heap, addr) {
                    Ok(w) ->
                      Advance(bind_sigma(
                        ctx,
                        s,
                        VarRef(heap.paired_reader(ctx.heap, w)),
                      ))
                    Error(_) -> Advance(bind_sigma(ctx, s, VarRef(addr)))
                  }
                _ -> Advance(ctx)
              }
          }
        _, _ -> Advance(ctx)
      }
    ConstTerm(c) ->
      case stored {
        CVAddr(s) -> Advance(bind_sigma(ctx, s, arg))
        CVTerm(ConstTerm(sc)) ->
          case sc == c {
            True -> Advance(ctx)
            False -> soft_fail(program, ctx, pc)
          }
        _ -> Advance(ctx)
      }
    StructTerm(f, _) ->
      case stored {
        CVAddr(s) -> Advance(bind_sigma(ctx, s, arg))
        CVTerm(StructTerm(sf, _)) ->
          case sf == f {
            True -> Advance(ctx)
            False -> soft_fail(program, ctx, pc)
          }
        _ -> Advance(ctx)
      }
  }
}

fn get_value_reader(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  arg: Term,
  stored: CVar,
) -> Step {
  case arg {
    VarRef(addr) ->
      case heap.is_writer(ctx.heap, addr), heap.is_reader(ctx.heap, addr) {
        // Goal writer, head reader: bind goal writer to the stored value.
        True, _ ->
          case stored {
            CVTerm(t) -> Advance(bind_sigma(ctx, addr, t))
            CVAddr(s) ->
              case dval(ctx.heap, s) {
                Bound(v) -> Advance(bind_sigma(ctx, addr, v))
                Unbound(terminal) ->
                  soft_fail_with(
                    program,
                    RunnerContext(..ctx, u: set.insert(ctx.u, terminal)),
                    pc,
                  )
              }
            _ -> soft_fail(program, ctx, pc)
          }
        // Goal reader, head reader: reader identity (compare addresses).
        _, True -> {
          let compare_to = case heap.paired_writer(ctx.heap, addr) {
            Ok(w) -> w
            Error(_) -> addr
          }
          case stored {
            CVAddr(s) ->
              case compare_to == s {
                True -> Advance(ctx)
                False -> soft_fail(program, ctx, pc)
              }
            _ -> Advance(ctx)
          }
        }
        _, _ -> Advance(ctx)
      }
    // Ground term: identity against the stored value.
    _ ->
      case stored {
        CVTerm(t) ->
          case t == arg {
            True -> Advance(ctx)
            False -> soft_fail(program, ctx, pc)
          }
        _ -> soft_fail(program, ctx, pc)
      }
  }
}

// ── HeadStructure (Dart runner.dart:1222) ────────────────────────────────────
//
// Match/build a structure at the argument (or, for arg_slot ≥ 10, the clause
// variable). Bound matching structure → READ mode; unbound writer → build a
// tentative structure in σ̂w and enter WRITE mode; unbound reader → defer via Si.

fn head_structure(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  functor: String,
  arity: Int,
  arg_slot: Int,
) -> Step {
  case arg_slot >= 10 {
    // Clause variable (a nested structure extracted into a temp).
    True ->
      case dict.get(ctx.clause_vars, arg_slot) {
        Error(_) -> soft_fail(program, ctx, pc)
        Ok(CVAddr(wid)) ->
          head_structure_writer(program, ctx, pc, functor, arity, wid)
        Ok(CVTerm(VarRef(addr))) ->
          case heap.is_writer(ctx.heap, addr), heap.is_reader(ctx.heap, addr) {
            True, _ ->
              head_structure_writer(program, ctx, pc, functor, arity, addr)
            _, True ->
              head_structure_reader(program, ctx, pc, functor, arity, addr)
            _, _ -> soft_fail(program, ctx, pc)
          }
        Ok(CVTerm(StructTerm(f, args))) ->
          match_struct_value(program, ctx, pc, functor, arity, f, args)
        Ok(_) -> soft_fail(program, ctx, pc)
      }
    // Real argument register.
    False ->
      case program.get_reg(ctx.regs, arg_slot) {
        Error(_) -> soft_fail(program, ctx, pc)
        Ok(VarRef(addr)) ->
          case heap.is_writer(ctx.heap, addr), heap.is_reader(ctx.heap, addr) {
            True, _ ->
              head_structure_writer(program, ctx, pc, functor, arity, addr)
            _, True ->
              head_structure_reader(program, ctx, pc, functor, arity, addr)
            // A value cell reached directly.
            _, _ ->
              case dval(ctx.heap, addr) {
                Bound(StructTerm(f, args)) ->
                  match_struct_value(program, ctx, pc, functor, arity, f, args)
                _ -> soft_fail(program, ctx, pc)
              }
          }
        // Ground term supplied directly (test/query shortcut).
        Ok(StructTerm(f, args)) ->
          match_struct_value(program, ctx, pc, functor, arity, f, args)
        Ok(_) -> soft_fail(program, ctx, pc)
      }
  }
}

fn head_structure_writer(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  functor: String,
  arity: Int,
  wid: Int,
) -> Step {
  case dval(ctx.heap, wid) {
    Bound(StructTerm(f, args)) ->
      match_struct_value(program, ctx, pc, functor, arity, f, args)
    Bound(_) -> soft_fail(program, ctx, pc)
    Unbound(_) -> Advance(enter_tentative(ctx, functor, arity, wid))
  }
}

fn head_structure_reader(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  functor: String,
  arity: Int,
  rid: Int,
) -> Step {
  case dval(ctx.heap, rid) {
    Bound(StructTerm(f, args)) ->
      match_struct_value(program, ctx, pc, functor, arity, f, args)
    Bound(_) -> soft_fail(program, ctx, pc)
    // Unbound reader: two-phase — defer via Si (on the terminal writer).
    Unbound(terminal) ->
      Advance(RunnerContext(..ctx, si: set.insert(ctx.si, terminal)))
  }
}

fn match_struct_value(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  functor: String,
  arity: Int,
  f: String,
  args: List(Term),
) -> Step {
  case f == functor && list.length(args) == arity {
    True ->
      Advance(
        RunnerContext(..ctx, current: BTStruct(f, args), mode: ReadMode, s: 0),
      )
    False -> soft_fail(program, ctx, pc)
  }
}

/// Create a fresh tentative structure for writer `wid`, record it in σ̂w, and
/// enter WRITE mode (Dart HeadStructure unbound-writer path).
fn enter_tentative(
  ctx: RunnerContext,
  functor: String,
  arity: Int,
  wid: Int,
) -> RunnerContext {
  let struct = TentStruct(functor, arity, list.repeat(TSVoid, arity))
  RunnerContext(
    ..ctx,
    sigma_hat: dict.insert(ctx.sigma_hat, wid, SVTentative(struct)),
    current: BTTentative(struct),
    current_writer: Some(wid),
    mode: WriteMode,
    s: 0,
  )
}

// ── UnifyVariable (Dart opv2.UnifyVariable, runner.dart:1834) ────────────────
//
// A structure-element clause variable at the current S position. WRITE mode
// (HEAD) fills a tentative slot; READ mode unifies the incoming subterm.

fn unify_variable(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  var_index: Int,
  is_reader: Bool,
) -> Step {
  case ctx.mode, ctx.current {
    WriteMode, BTTentative(struct) ->
      unify_variable_write(ctx, struct, var_index, is_reader)
    ReadMode, BTStruct(_, args) ->
      unify_variable_read(program, ctx, pc, args, var_index, is_reader)
    // WRITE into a real StructTerm is BODY construction — same as SetVariable.
    WriteMode, BTStruct(_, _) -> body_element_var(ctx, var_index, is_reader)
    WriteMode, BTNone -> Advance(ctx)
    ReadMode, _ -> Advance(ctx)
  }
}

fn unify_variable_write(
  ctx: RunnerContext,
  struct: TentStruct,
  var_index: Int,
  is_reader: Bool,
) -> Step {
  let #(ctx, slot) = write_slot_for_cvar(ctx, var_index, is_reader)
  let struct = TentStruct(..struct, args: list_set(struct.args, ctx.s, slot))
  Advance(RunnerContext(..put_current(ctx, BTTentative(struct)), s: ctx.s + 1))
}

/// The tentative slot for clause variable `var_index` in WRITE mode, allocating
/// a fresh variable on first occurrence (Dart runner.dart:1846-1907).
fn write_slot_for_cvar(
  ctx: RunnerContext,
  var_index: Int,
  is_reader: Bool,
) -> #(RunnerContext, TentSlot) {
  case dict.get(ctx.clause_vars, var_index) {
    Ok(CVTerm(VarRef(addr))) ->
      case heap.is_value(ctx.heap, addr) {
        True ->
          case dval(ctx.heap, addr) {
            Bound(ground) -> write_slot_ground(ctx, ground, is_reader)
            Unbound(_) -> #(ctx, TSTerm(VarRef(addr)))
          }
        False ->
          case
            is_reader,
            heap.is_writer(ctx.heap, addr),
            heap.is_reader(ctx.heap, addr)
          {
            True, True, _ -> #(
              ctx,
              TSTerm(VarRef(heap.paired_reader(ctx.heap, addr))),
            )
            False, _, True ->
              case heap.paired_writer(ctx.heap, addr) {
                Ok(w) -> #(ctx, TSTerm(VarRef(w)))
                Error(_) -> #(ctx, TSTerm(VarRef(addr)))
              }
            _, _, _ -> #(ctx, TSTerm(VarRef(addr)))
          }
      }
    Ok(CVAddr(a)) ->
      case is_reader {
        True -> #(ctx, TSTerm(VarRef(heap.paired_reader(ctx.heap, a))))
        False -> #(ctx, TSTerm(VarRef(a)))
      }
    Ok(CVTerm(ground)) -> write_slot_ground(ctx, ground, is_reader)
    Ok(CVTentative(nested)) -> #(ctx, TSNested(nested))
    // First occurrence: allocate a fresh variable, store the writer as the base.
    Error(_) -> {
      let #(heap, w, r) = heap.allocate_variable(ctx.heap)
      let ctx =
        RunnerContext(
          ..ctx,
          heap: heap,
          clause_vars: dict.insert(
            ctx.clause_vars,
            var_index,
            CVTerm(VarRef(w)),
          ),
        )
      let addr = case is_reader {
        True -> r
        False -> w
      }
      #(ctx, TSTerm(VarRef(addr)))
    }
    // _StructureState is never a UnifyVariable operand — fall back to a
    // placeholder resolved at Commit (Dart `_ClauseVar`).
    Ok(CVState(_, _, _, _)) -> #(ctx, TSClauseVar(var_index, !is_reader))
  }
}

/// A ground clause-var value placed into a WRITE slot: reader mode allocates a
/// fresh var bound tentatively to the ground term; writer mode uses it directly.
fn write_slot_ground(
  ctx: RunnerContext,
  ground: Term,
  is_reader: Bool,
) -> #(RunnerContext, TentSlot) {
  case is_reader {
    True -> {
      let #(heap, w, r) = heap.allocate_variable(ctx.heap)
      #(
        RunnerContext(
          ..ctx,
          heap: heap,
          sigma_hat: dict.insert(ctx.sigma_hat, w, SVTerm(ground)),
        ),
        TSTerm(VarRef(r)),
      )
    }
    False -> #(ctx, TSTerm(ground))
  }
}

fn unify_variable_read(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  args: List(Term),
  var_index: Int,
  is_reader: Bool,
) -> Step {
  case list_at(args, ctx.s) {
    Error(_) -> Advance(ctx)
    Ok(raw) -> {
      let value = deref_value(ctx, raw)
      let existing = dict.get(ctx.clause_vars, var_index)
      case is_reader {
        True ->
          unify_variable_read_reader(
            program,
            ctx,
            pc,
            value,
            var_index,
            existing,
          )
        False ->
          unify_variable_read_writer(
            program,
            ctx,
            pc,
            value,
            var_index,
            existing,
          )
      }
    }
  }
}

/// Deref a structure-arg term if it is a `VarRef` pointing at a value cell (Dart
/// runner.dart:2085); otherwise leave it as-is (unbound var refs stay refs).
fn deref_value(ctx: RunnerContext, term: Term) -> Term {
  case term {
    VarRef(addr) ->
      case heap.is_value(ctx.heap, addr) {
        True ->
          case dval(ctx.heap, addr) {
            Bound(v) -> v
            Unbound(_) -> term
          }
        False -> term
      }
    _ -> term
  }
}

fn unify_variable_read_reader(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  value: Term,
  var_index: Int,
  existing: Result(CVar, Nil),
) -> Step {
  case value {
    VarRef(addr) ->
      case heap.is_reader(ctx.heap, addr), heap.is_writer(ctx.heap, addr) {
        // Reader × Reader = FAIL.
        True, _ -> soft_fail(program, ctx, pc)
        // Query writer, head reader.
        _, True ->
          case existing {
            Ok(CVTerm(ConstTerm(_)) as gv) ->
              bind_existing_ground(ctx, addr, gv)
            Ok(CVTerm(StructTerm(_, _)) as gv) ->
              bind_existing_ground(ctx, addr, gv)
            Ok(CVTerm(VarRef(e))) -> {
              let reader = case heap.is_writer(ctx.heap, e) {
                True -> heap.paired_reader(ctx.heap, e)
                False -> e
              }
              Advance(advance_s(bind_sigma(ctx, addr, VarRef(reader))))
            }
            Ok(CVAddr(e)) ->
              Advance(
                advance_s(bind_sigma(
                  ctx,
                  addr,
                  VarRef(heap.paired_reader(ctx.heap, e)),
                )),
              )
            // First occurrence: head reader receives the goal writer addr.
            Error(_) ->
              Advance(advance_s(set_cvar(ctx, var_index, CVAddr(addr))))
            Ok(_) -> Advance(advance_s(ctx))
          }
        _, _ -> soft_fail(program, ctx, pc)
      }
    // Ground term, clause expects reader: fresh var bound to it in σ̂w.
    ConstTerm(_) | StructTerm(_, _) -> {
      let #(heap, w, _) = heap.allocate_variable(ctx.heap)
      let ctx =
        RunnerContext(
          ..ctx,
          heap: heap,
          sigma_hat: dict.insert(ctx.sigma_hat, w, SVTerm(value)),
        )
      Advance(advance_s(set_cvar(ctx, var_index, CVAddr(w))))
    }
  }
}

fn bind_existing_ground(ctx: RunnerContext, addr: Int, gv: CVar) -> Step {
  case gv {
    CVTerm(t) -> Advance(advance_s(bind_sigma(ctx, addr, t)))
    _ -> Advance(advance_s(ctx))
  }
}

fn unify_variable_read_writer(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  value: Term,
  var_index: Int,
  existing: Result(CVar, Nil),
) -> Step {
  // Clause var already a fresh writer addr from a previous UnifyReader?
  case clausevar_writer_addr(ctx, existing) {
    Ok(clause_addr) ->
      case value {
        VarRef(addr) ->
          case heap.is_writer(ctx.heap, addr), heap.is_reader(ctx.heap, addr) {
            True, _ ->
              // WxW: two unbound writers cannot be equated.
              case
                heap_bound(ctx.heap, clause_addr) || heap_bound(ctx.heap, addr)
              {
                False -> soft_fail(program, ctx, pc)
                True -> Advance(advance_s(bind_sigma(ctx, clause_addr, value)))
              }
            _, True -> Advance(advance_s(bind_sigma(ctx, clause_addr, value)))
            _, _ -> soft_fail(program, ctx, pc)
          }
        ConstTerm(_) | StructTerm(_, _) ->
          Advance(advance_s(bind_sigma(ctx, clause_addr, value)))
      }
    Error(_) ->
      case existing {
        // Clause var already bound — advance.
        Ok(_) -> Advance(advance_s(ctx))
        // First occurrence — store the value.
        Error(_) ->
          case value {
            VarRef(addr) ->
              case
                heap.is_writer(ctx.heap, addr),
                heap.is_reader(ctx.heap, addr)
              {
                True, _ ->
                  Advance(
                    advance_s(set_cvar(ctx, var_index, CVTerm(VarRef(addr)))),
                  )
                _, True ->
                  case dval(ctx.heap, addr) {
                    Bound(rv) ->
                      Advance(advance_s(set_cvar(ctx, var_index, CVTerm(rv))))
                    Unbound(_) ->
                      Advance(
                        advance_s(set_cvar(ctx, var_index, CVTerm(VarRef(addr)))),
                      )
                  }
                _, _ -> soft_fail(program, ctx, pc)
              }
            ConstTerm(_) | StructTerm(_, _) ->
              Advance(advance_s(set_cvar(ctx, var_index, CVTerm(value))))
          }
      }
  }
}

/// The clause-var's writer addr when it holds a fresh writer (bare addr or
/// writer VarRef) — the Dart `existingValue is int || (VarRef && isWriter)`.
fn clausevar_writer_addr(
  ctx: RunnerContext,
  existing: Result(CVar, Nil),
) -> Result(Int, Nil) {
  case existing {
    Ok(CVAddr(e)) -> Ok(e)
    Ok(CVTerm(VarRef(e))) ->
      case heap.is_writer(ctx.heap, e) {
        True -> Ok(e)
        False -> Error(Nil)
      }
    _ -> Error(Nil)
  }
}

// ── UnifyConstant (Dart runner.dart:1660) ────────────────────────────────────

fn unify_constant(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  value: Constant,
) -> Step {
  case ctx.mode, ctx.current {
    // WRITE (HEAD): store the constant into the tentative slot.
    WriteMode, BTTentative(struct) -> {
      let struct =
        TentStruct(
          ..struct,
          args: list_set(struct.args, ctx.s, TSTerm(ConstTerm(value))),
        )
      Advance(
        RunnerContext(..put_current(ctx, BTTentative(struct)), s: ctx.s + 1),
      )
    }
    // WRITE into a real StructTerm is BODY construction — same as SetConstant.
    WriteMode, BTStruct(_, _) -> body_element_const(ctx, value)
    WriteMode, BTNone -> Advance(ctx)
    // READ: verify the value at S matches the constant.
    ReadMode, BTStruct(_, args) ->
      case list_at(args, ctx.s) {
        Error(_) -> soft_fail(program, ctx, pc)
        Ok(ConstTerm(c)) ->
          case c == value {
            True -> Advance(advance_s(ctx))
            False -> soft_fail(program, ctx, pc)
          }
        Ok(VarRef(addr)) ->
          case heap.is_writer(ctx.heap, addr), heap.is_reader(ctx.heap, addr) {
            True, _ ->
              case dval(ctx.heap, addr) {
                Bound(ConstTerm(c)) ->
                  case c == value {
                    True -> Advance(advance_s(ctx))
                    False -> soft_fail(program, ctx, pc)
                  }
                Bound(_) -> soft_fail(program, ctx, pc)
                Unbound(_) ->
                  Advance(advance_s(bind_sigma(ctx, addr, ConstTerm(value))))
              }
            _, True ->
              case dval(ctx.heap, addr) {
                Bound(ConstTerm(c)) ->
                  case c == value {
                    True -> Advance(advance_s(ctx))
                    False -> soft_fail(program, ctx, pc)
                  }
                Bound(_) -> soft_fail(program, ctx, pc)
                Unbound(terminal) ->
                  Advance(advance_s(
                    RunnerContext(..ctx, si: set.insert(ctx.si, terminal)),
                  ))
              }
            _, _ -> soft_fail(program, ctx, pc)
          }
        Ok(_) -> soft_fail(program, ctx, pc)
      }
    // Not a structure (e.g. HeadStructure added the arg to Si) — skip.
    ReadMode, _ -> Advance(ctx)
  }
}

// ── UnifyVoid (Dart runner.dart:1815) ────────────────────────────────────────

fn unify_void(ctx: RunnerContext, count: Int) -> Step {
  case ctx.mode, ctx.current {
    WriteMode, BTTentative(struct) -> {
      let #(args, s) = fill_void(struct.args, ctx.s, count)
      Advance(
        RunnerContext(
          ..put_current(ctx, BTTentative(TentStruct(..struct, args: args))),
          s: s,
        ),
      )
    }
    WriteMode, _ -> Advance(ctx)
    // READ: skip over `count` positions.
    ReadMode, _ -> Advance(RunnerContext(..ctx, s: ctx.s + count))
  }
}

fn fill_void(
  args: List(TentSlot),
  s: Int,
  count: Int,
) -> #(List(TentSlot), Int) {
  case count <= 0 || s >= list.length(args) {
    True -> #(args, s)
    False -> fill_void(list_set(args, s, TSVoid), s + 1, count - 1)
  }
}

// ── Push / Pop (Dart runner.dart:892 / 904) ──────────────────────────────────

fn push(ctx: RunnerContext, reg_index: Int) -> Step {
  Advance(set_cvar(
    ctx,
    reg_index,
    CVState(ctx.s, ctx.mode, ctx.current, ctx.current_writer),
  ))
}

fn pop(ctx: RunnerContext, reg_index: Int) -> Step {
  case dict.get(ctx.clause_vars, reg_index) {
    Ok(CVState(saved_s, saved_mode, saved_target, saved_writer)) -> {
      // Save the built nested structure to the register (Dart Pop), then
      // restore the parent traversal state.
      let built = case ctx.current {
        BTTentative(t) -> CVTentative(t)
        BTStruct(f, a) -> CVTerm(StructTerm(f, a))
        BTNone -> CVAddr(-1)
      }
      let ctx = set_cvar(ctx, reg_index, built)
      Advance(
        RunnerContext(
          ..ctx,
          s: saved_s,
          mode: saved_mode,
          current: saved_target,
          current_writer: saved_writer,
        ),
      )
    }
    _ -> Advance(ctx)
  }
}

// ── UnifyStructure (Dart runner.dart:923) ────────────────────────────────────
//
// A nested structure at the current S position.

fn unify_structure(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  functor: String,
  arity: Int,
) -> Step {
  case ctx.mode, ctx.current {
    ReadMode, BTStruct(_, args) ->
      case list_at(args, ctx.s) {
        Error(_) -> Advance(ctx)
        Ok(raw) -> unify_structure_read(program, ctx, pc, functor, arity, raw)
      }
    // WRITE: enter a fresh nested tentative (placement handled by Pop +
    // UnifyVariable(save); the parent is preserved by the enclosing Push).
    WriteMode, BTTentative(_) -> {
      let nested = TentStruct(functor, arity, list.repeat(TSVoid, arity))
      Advance(
        RunnerContext(
          ..ctx,
          current: BTTentative(nested),
          current_writer: None,
          s: 0,
        ),
      )
    }
    _, _ -> Advance(ctx)
  }
}

fn unify_structure_read(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  functor: String,
  arity: Int,
  raw: Term,
) -> Step {
  // Prefer a σ̂w tentative binding, then the heap (Dart runner.dart:942). The heap
  // fallback must use `dval` (full deref), NOT `deref_value` — a nested struct arg
  // (e.g. the `p(a,b)` inside a goal `w(p(a,b))`) is materialised by goal-boot as a
  // BOUND READER, which `deref_value`'s `is_value`-only gate leaves as a bare `VarRef`,
  // making a nested head match (`w(p(_,_))`) wrongly soft-fail. `dval` follows a bound
  // reader/writer/value cell to its term (an unbound cell stays a `VarRef`, so the
  // writer→mode-convert / reader→suspend logic below is unchanged).
  let value = case raw {
    VarRef(addr) ->
      case dict.get(ctx.sigma_hat, addr) {
        Ok(SVTerm(v)) -> v
        Ok(SVTentative(_)) -> raw
        Error(_) ->
          case dval(ctx.heap, addr) {
            Bound(v) -> v
            Unbound(_) -> raw
          }
      }
    _ -> raw
  }
  case value {
    StructTerm(f, args) ->
      case f == functor && list.length(args) == arity {
        True -> Advance(RunnerContext(..ctx, current: BTStruct(f, args), s: 0))
        False -> soft_fail(program, ctx, pc)
      }
    VarRef(addr) ->
      case heap.is_writer(ctx.heap, addr), heap.is_reader(ctx.heap, addr) {
        // Mode conversion: unbound writer where a structure is expected.
        True, _ -> {
          let nested = TentStruct(functor, arity, list.repeat(TSVoid, arity))
          Advance(
            RunnerContext(
              ..ctx,
              sigma_hat: dict.insert(ctx.sigma_hat, addr, SVTentative(nested)),
              current: BTTentative(nested),
              current_writer: Some(addr),
              mode: WriteMode,
              s: 0,
            ),
          )
        }
        // Unbound reader where a structure is expected: suspend (Dart adds to U).
        _, True ->
          soft_fail_with(
            program,
            RunnerContext(..ctx, u: set.insert(ctx.u, addr)),
            pc,
          )
        _, _ -> soft_fail(program, ctx, pc)
      }
    _ -> soft_fail(program, ctx, pc)
  }
}

// ── BODY argument construction + spawn (slice 21d) ───────────────────────────
//
// After Commit (`in_body`), the body builds each goal's argument registers
// (`arg_slots`) and spawns goals. Structures are built on the heap incrementally
// (`current` = a real `BTStruct`, filled by SetVariable/SetConstant and the
// top-level UnifyVariable/UnifyConstant), binding the target writer on
// completion and unwinding nested `parent_stack` frames.

/// A transient placeholder for an unfilled BODY structure slot (Dart
/// `ConstTerm(null)`); always overwritten before the structure completes (S
/// walks every position), so it never reaches a bound term.
fn body_placeholder() -> Term {
  ConstTerm(ConstAtom("$unset"))
}

fn set_arg(ctx: RunnerContext, slot: Int, term: Term) -> RunnerContext {
  RunnerContext(..ctx, arg_slots: dict.insert(ctx.arg_slots, slot, term))
}

// PutVariable (Dart opv2.PutVariable, runner.dart:2971) — place clause var Xi
// into arg_slots[argSlot] (mode-corrected); alloc fresh on first occurrence.
fn put_variable(
  ctx: RunnerContext,
  var_index: Int,
  arg_slot: Int,
  is_reader: Bool,
) -> Step {
  case dict.get(ctx.clause_vars, var_index) {
    Ok(CVTerm(VarRef(addr))) ->
      Advance(set_arg(ctx, arg_slot, VarRef(mode_addr(ctx, addr, is_reader))))
    Ok(CVAddr(v)) -> {
      let a = case is_reader {
        True -> heap.paired_reader(ctx.heap, v)
        False -> v
      }
      Advance(set_arg(ctx, arg_slot, VarRef(a)))
    }
    Ok(CVTerm(ground)) ->
      case is_reader {
        // Reader mode: alloc a fresh var bound to the ground term, pass its
        // reader (CallEnv arguments are VarRefs — Dart runner.dart:2987).
        True ->
          case heap.allocate_variable(ctx.heap) {
            #(h, w, r) ->
              case heap.bind_writer(h, w, ground) {
                Ok(#(h2, fired)) ->
                  Advance(set_arg(
                    RunnerContext(
                      ..ctx,
                      heap: h2,
                      woken: list.append(ctx.woken, fired),
                    ),
                    arg_slot,
                    VarRef(r),
                  ))
                Error(e) ->
                  Stop(RunnerError(StructuralViolation(heap_error_detail(e))))
              }
          }
        False -> Advance(set_arg(ctx, arg_slot, ground))
      }
    // First occurrence: allocate a fresh variable, store the writer base.
    Error(_) -> {
      let #(h, w, r) = heap.allocate_variable(ctx.heap)
      let ctx =
        RunnerContext(
          ..ctx,
          heap: h,
          clause_vars: dict.insert(
            ctx.clause_vars,
            var_index,
            CVTerm(VarRef(w)),
          ),
        )
      let a = case is_reader {
        True -> r
        False -> w
      }
      Advance(set_arg(ctx, arg_slot, VarRef(a)))
    }
    _ -> Advance(ctx)
  }
}

/// The address for a VarRef in `is_reader` mode: a writer viewed as a reader
/// yields its paired reader; a reader viewed as a writer yields its paired
/// writer; a value cell is used as-is (Dart PutVariable/SetVariable mode logic).
fn mode_addr(ctx: RunnerContext, addr: Int, is_reader: Bool) -> Int {
  case
    is_reader,
    heap.is_writer(ctx.heap, addr),
    heap.is_reader(ctx.heap, addr)
  {
    True, True, _ -> heap.paired_reader(ctx.heap, addr)
    False, _, True ->
      case heap.paired_writer(ctx.heap, addr) {
        Ok(w) -> w
        Error(_) -> addr
      }
    _, _, _ -> addr
  }
}

// PutConstant / PutBoundConst / PutNil / PutBoundNil (Dart 3048/4470/4458/4480) —
// alloc a fresh var bound to the constant, store its reader in arg_slots.
fn put_bound_const(ctx: RunnerContext, value: Constant, arg_slot: Int) -> Step {
  let #(h, w, r) = heap.allocate_variable(ctx.heap)
  case heap.bind_writer(h, w, ConstTerm(value)) {
    Ok(#(h2, fired)) ->
      Advance(set_arg(
        RunnerContext(..ctx, heap: h2, woken: list.append(ctx.woken, fired)),
        arg_slot,
        VarRef(r),
      ))
    Error(e) -> Stop(RunnerError(StructuralViolation(heap_error_detail(e))))
  }
}

// PutStructure (Dart runner.dart:3058) — begin a BODY structure. Alloc a fresh
// target writer, push the parent frame if nested, record the target slot.
fn put_structure(
  ctx: RunnerContext,
  functor: String,
  arity: Int,
  arg_slot: Int,
) -> Step {
  let #(h, w, _r) = heap.allocate_variable(ctx.heap)
  let ctx = RunnerContext(..ctx, heap: h)
  // A NESTED structure element (arg_slot == -1) pushes the in-progress parent so
  // the completed child is placed back into the parent slot on unwind. A TOP-LEVEL
  // argument (arg_slot >= 0) starts a FRESH structure: any `current`/`parent_stack`
  // is stale build state left over from the HEAD phase (e.g. reading a `[X|Xs]` head
  // leaves `current = BTStruct(".")`), and must NOT be treated as a parent — else the
  // completed operand's reader is redirected into the stale head structure instead of
  // `arg_slots[slot]` (the guard-operand `mod(X,P)` bug: it resolved to just `P`).
  let ctx = case arg_slot == -1 {
    True ->
      RunnerContext(..ctx, parent_stack: [
        ParentCtx(ctx.current, ctx.s, ctx.mode, ctx.build_writer),
        ..ctx.parent_stack
      ])
    False -> RunnerContext(..ctx, parent_stack: [])
  }
  let ctx = RunnerContext(..ctx, build_writer: Some(w))
  let ctx = case arg_slot >= 0 && arg_slot < 10, arg_slot >= 10 {
    True, _ -> RunnerContext(..ctx, build_slot: Some(arg_slot))
    _, True ->
      RunnerContext(
        ..ctx,
        clause_vars: dict.insert(ctx.clause_vars, arg_slot, CVTerm(VarRef(w))),
      )
    _, _ -> ctx
  }
  Advance(
    RunnerContext(
      ..ctx,
      current: BTStruct(functor, list.repeat(body_placeholder(), arity)),
      s: 0,
      mode: WriteMode,
    ),
  )
}

// SetVariable / body UnifyVariable (Dart opv2.SetVariable, runner.dart:2522) —
// place clause var Xi into the current BODY structure at S; complete on fill.
fn body_element_var(
  ctx: RunnerContext,
  var_index: Int,
  is_reader: Bool,
) -> Step {
  case ctx.current {
    BTStruct(f, args) -> {
      let #(ctx, term) = body_var_term(ctx, var_index, is_reader)
      let ctx =
        RunnerContext(
          ..ctx,
          current: BTStruct(f, list_set(args, ctx.s, term)),
          s: ctx.s + 1,
        )
      maybe_complete_body(ctx)
    }
    _ -> Advance(ctx)
  }
}

/// The term to place for clause var `var_index` in a BODY structure slot,
/// allocating a fresh variable on first occurrence (Dart SetVariable cases).
fn body_var_term(
  ctx: RunnerContext,
  var_index: Int,
  is_reader: Bool,
) -> #(RunnerContext, Term) {
  case dict.get(ctx.clause_vars, var_index) {
    Ok(CVTerm(VarRef(addr))) -> #(ctx, VarRef(mode_addr(ctx, addr, is_reader)))
    Ok(CVAddr(v)) -> {
      let a = case is_reader {
        True -> heap.paired_reader(ctx.heap, v)
        False -> v
      }
      #(ctx, VarRef(a))
    }
    // Ground term: embed directly.
    Ok(CVTerm(ground)) -> #(ctx, ground)
    _ -> {
      let #(h, w, r) = heap.allocate_variable(ctx.heap)
      let ctx =
        RunnerContext(
          ..ctx,
          heap: h,
          clause_vars: dict.insert(
            ctx.clause_vars,
            var_index,
            CVTerm(VarRef(w)),
          ),
        )
      let a = case is_reader {
        True -> r
        False -> w
      }
      #(ctx, VarRef(a))
    }
  }
}

// SetConstant / body UnifyConstant (Dart runner.dart:3109) — place a constant
// into the current BODY structure at S; complete on fill.
fn body_element_const(ctx: RunnerContext, value: Constant) -> Step {
  case ctx.current {
    BTStruct(f, args) -> {
      let ctx =
        RunnerContext(
          ..ctx,
          current: BTStruct(f, list_set(args, ctx.s, ConstTerm(value))),
          s: ctx.s + 1,
        )
      maybe_complete_body(ctx)
    }
    _ -> Advance(ctx)
  }
}

fn maybe_complete_body(ctx: RunnerContext) -> Step {
  case ctx.current {
    BTStruct(_, args) ->
      case ctx.s >= list.length(args) {
        True -> complete_body_struct(ctx)
        False -> Advance(ctx)
      }
    _ -> Advance(ctx)
  }
}

/// A completed BODY structure: bind its target writer to the `StructTerm`, then
/// unwind the parent stack (recursively completing ancestors), finally storing
/// the outermost structure's reader into `arg_slots` (Dart Set* completion).
fn complete_body_struct(ctx: RunnerContext) -> Step {
  case ctx.build_writer, ctx.current {
    Some(wid), BTStruct(f, args) ->
      case bind_body_writer(ctx, wid, f, args) {
        Error(fault) -> Stop(RunnerError(fault))
        Ok(ctx) -> unwind_body(ctx, wid)
      }
    _, _ -> Advance(ctx)
  }
}

fn bind_body_writer(
  ctx: RunnerContext,
  wid: Int,
  functor: String,
  args: List(Term),
) -> Result(RunnerContext, RunnerFault) {
  case heap.bind_writer(ctx.heap, wid, StructTerm(functor, args)) {
    Ok(#(h, fired)) ->
      Ok(RunnerContext(..ctx, heap: h, woken: list.append(ctx.woken, fired)))
    Error(e) -> Error(StructuralViolation(heap_error_detail(e)))
  }
}

fn unwind_body(ctx: RunnerContext, completed_writer: Int) -> Step {
  case ctx.parent_stack {
    // Outermost structure done — store its reader in arg_slots, reset.
    [] -> {
      let ctx = case ctx.build_slot {
        Some(slot) ->
          set_arg(
            ctx,
            slot,
            VarRef(heap.paired_reader(ctx.heap, completed_writer)),
          )
        None -> ctx
      }
      Advance(
        RunnerContext(
          ..ctx,
          current: BTNone,
          mode: ReadMode,
          s: 0,
          build_writer: None,
          build_slot: None,
        ),
      )
    }
    // Place the completed structure's reader into the parent slot, restore the
    // parent, and (if it is now complete) recurse.
    [parent, ..rest] -> {
      let reader = VarRef(heap.paired_reader(ctx.heap, completed_writer))
      let parent_struct = case parent.structure {
        BTStruct(pf, pargs) -> BTStruct(pf, list_set(pargs, parent.s, reader))
        other -> other
      }
      let ctx =
        RunnerContext(
          ..ctx,
          current: parent_struct,
          s: parent.s + 1,
          mode: parent.mode,
          build_writer: parent.writer,
          parent_stack: rest,
        )
      case ctx.current, ctx.build_writer {
        BTStruct(pf, pargs), Some(pw) ->
          case ctx.s >= list.length(pargs) {
            True ->
              case bind_body_writer(ctx, pw, pf, pargs) {
                Error(fault) -> Stop(RunnerError(fault))
                Ok(ctx) -> unwind_body(ctx, pw)
              }
            False -> Advance(ctx)
          }
        _, _ -> Advance(ctx)
      }
    }
  }
}

// Spawn (Dart runner.dart:3220) — emit a spawn request for the scheduler with
// the body-built argument registers. A missing procedure label is a body kernel
// (T024) — surfaced, not silently dropped.
fn spawn(
  program: BytecodeProgram,
  ctx: RunnerContext,
  label: LabelName,
  arity: Int,
) -> Step {
  case program.label_pc(program, label) {
    Ok(entry_pc) -> {
      let regs = build_spawn_regs(ctx.arg_slots, arity)
      Advance(
        RunnerContext(
          ..ctx,
          spawn_reqs: list.append(ctx.spawn_reqs, [
            SpawnReq(label, entry_pc, regs),
          ]),
          arg_slots: dict.new(),
        ),
      )
    }
    // Label miss → a BODY kernel executed INLINE (Dart runner.dart:3226-3252):
    // strip any `/arity` suffix, dispatch heap-only, bind the output writer.
    Error(_) -> {
      let proc_name = case string.split(label, "/") {
        [head, ..] -> head
        [] -> label
      }
      let args =
        upto(arity)
        |> list.map(fn(i) {
          case dict.get(ctx.arg_slots, i) {
            Ok(t) -> t
            Error(_) -> ConstTerm(ConstAtom("$missing"))
          }
        })
      case kernels.dispatch(ctx.heap, proc_name, arity, args) {
        Ok(kernels.KSuccess(heap, woken, output)) ->
          Advance(
            RunnerContext(
              ..ctx,
              heap: heap,
              woken: list.append(ctx.woken, woken),
              output: list.append(ctx.output, output),
              arg_slots: dict.new(),
            ),
          )
        // A kernel abort is a fatal type error (Dart RunResult.terminated) —
        // guards should have prevented it; surface it, never swallow.
        Ok(kernels.KAbort(detail)) ->
          Stop(
            RunnerError(Malformed(
              "body kernel " <> label <> " aborted: " <> detail,
            )),
          )
        // Neither a program label nor a registered kernel.
        Error(_) ->
          Stop(
            RunnerError(Malformed(
              "spawn: unresolved procedure/kernel " <> label,
            )),
          )
      }
    }
  }
}

fn build_spawn_regs(arg_slots: Dict(Int, Term), arity: Int) -> XRegs {
  dict.fold(arg_slots, program.new_regs(), fn(regs, slot, term) {
    case slot >= 0 && slot < arity {
      True -> program.set_reg(regs, slot, term)
      False -> regs
    }
  })
}

// ── GUARD phase: pure three-valued tests (T023) ──────────────────────────────
//
// Guards are pure over σ̂w + the heap: SUCCEED (advance), SUSPEND (defer on the
// unbound readers' writers → U, soft-fail), or FAIL (soft-fail). Negation inverts
// success↔failure; suspension is unchanged. Implemented in the runner rather than
// a separate `guards.gleam` because they read the runner's σ̂w/clause-var state —
// a separate module would need those types and would import-cycle with the
// runner's dispatch (the shared-state extraction to `state.gleam` is a follow-up).

/// The accumulator for the cycle-safe collect-unbound walk (Dart `collectUnbound`
/// / `collectReaders`): whether an unbound writer was seen, the set of terminal
/// writers of unbound readers (the addresses to suspend on — writer-keyed
/// adaptation), and the visited set.
type Collect {
  Collect(has_writer: Bool, readers: Set(Int), visited: Set(Int))
}

fn empty_collect() -> Collect {
  Collect(has_writer: False, readers: set.new(), visited: set.new())
}

fn collect_cvar(ctx: RunnerContext, cvar: CVar, c: Collect) -> Collect {
  case cvar {
    CVAddr(addr) -> collect_addr(ctx, addr, c)
    CVTerm(t) -> collect_term(ctx, t, c)
    CVTentative(tent) -> collect_tent(ctx, tent, c)
    CVState(_, _, _, _) -> c
  }
}

fn collect_term(ctx: RunnerContext, term: Term, c: Collect) -> Collect {
  case term {
    VarRef(addr) -> collect_addr(ctx, addr, c)
    StructTerm(_, args) ->
      list.fold(args, c, fn(c, a) { collect_term(ctx, a, c) })
    ConstTerm(_) -> c
  }
}

fn collect_addr(ctx: RunnerContext, addr: Int, c: Collect) -> Collect {
  case set.contains(c.visited, addr) {
    True -> c
    False -> {
      let c = Collect(..c, visited: set.insert(c.visited, addr))
      case dict.get(ctx.sigma_hat, addr) {
        Ok(SVTerm(t)) -> collect_term(ctx, t, c)
        Ok(SVTentative(tent)) -> collect_tent(ctx, tent, c)
        Error(_) ->
          case heap.is_writer(ctx.heap, addr) {
            True ->
              case dval(ctx.heap, addr) {
                Bound(v) -> collect_term(ctx, v, c)
                Unbound(_) -> Collect(..c, has_writer: True)
              }
            False ->
              case dval(ctx.heap, addr) {
                Bound(v) -> collect_term(ctx, v, c)
                Unbound(terminal) ->
                  Collect(..c, readers: set.insert(c.readers, terminal))
              }
          }
      }
    }
  }
}

fn collect_tent(ctx: RunnerContext, tent: TentStruct, c: Collect) -> Collect {
  list.fold(tent.args, c, fn(c, slot) {
    case slot {
      TSTerm(t) -> collect_term(ctx, t, c)
      TSNested(nested) -> collect_tent(ctx, nested, c)
      TSVoid -> c
      TSClauseVar(_, _) -> c
    }
  })
}

/// `ground(X)` (Dart runner.dart:3656): unbound writer → FAIL, unbound readers →
/// SUSPEND, else SUCCEED. Negation inverts success↔failure.
fn guard_ground(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  var_index: Int,
  negated: Bool,
) -> Step {
  case dict.get(ctx.clause_vars, var_index) {
    Error(_) -> soft_fail(program, ctx, pc)
    Ok(cvar) -> {
      let c = collect_cvar(ctx, cvar, empty_collect())
      let has_readers = !set.is_empty(c.readers)
      case negated, c.has_writer, has_readers {
        _, _, True -> guard_suspend(program, ctx, pc, c.readers)
        False, True, _ -> soft_fail(program, ctx, pc)
        False, False, _ -> Advance(ctx)
        True, True, _ -> Advance(ctx)
        True, False, _ -> soft_fail(program, ctx, pc)
      }
    }
  }
}

/// The known/unknown classification of X itself (not subterms).
type Knownness {
  KKnown
  KUnboundReader(terminal: Int)
  KUnboundWriter
}

fn classify(ctx: RunnerContext, cvar: CVar) -> Knownness {
  case cvar {
    CVTerm(ConstTerm(_)) -> KKnown
    CVTerm(StructTerm(_, _)) -> KKnown
    CVTentative(_) -> KKnown
    CVState(_, _, _, _) -> KKnown
    CVTerm(VarRef(addr)) -> classify_addr(ctx, addr)
    CVAddr(addr) -> classify_addr(ctx, addr)
  }
}

fn classify_addr(ctx: RunnerContext, addr: Int) -> Knownness {
  case dict.has_key(ctx.sigma_hat, addr) {
    True -> KKnown
    False ->
      case heap.is_writer(ctx.heap, addr) {
        True ->
          case dval(ctx.heap, addr) {
            Bound(_) -> KKnown
            Unbound(_) -> KUnboundWriter
          }
        False ->
          case dval(ctx.heap, addr) {
            Bound(_) -> KKnown
            Unbound(terminal) -> KUnboundReader(terminal)
          }
      }
  }
}

/// `known(X)` (Dart runner.dart:3796): bound → SUCCEED, unbound reader → SUSPEND,
/// unbound writer → FAIL. Negation inverts success↔failure.
fn guard_known(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  var_index: Int,
  negated: Bool,
) -> Step {
  case dict.get(ctx.clause_vars, var_index) {
    Error(_) -> soft_fail(program, ctx, pc)
    Ok(cvar) ->
      case classify(ctx, cvar), negated {
        KUnboundReader(w), _ ->
          guard_suspend(program, ctx, pc, set.insert(set.new(), w))
        KKnown, False -> Advance(ctx)
        KKnown, True -> soft_fail(program, ctx, pc)
        KUnboundWriter, False -> soft_fail(program, ctx, pc)
        KUnboundWriter, True -> Advance(ctx)
      }
  }
}

/// `unknown(X)` (Dart opv2.Unknown, runner.dart:1017): SUCCEED iff X is unbound.
/// (Never emitted by the current codegen — `unknown` routes through the generic
/// Guard — but kept for completeness.)
fn guard_unknown(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  var_index: Int,
) -> Step {
  case dict.get(ctx.clause_vars, var_index) {
    Error(_) -> Advance(ctx)
    Ok(cvar) ->
      case classify(ctx, cvar) {
        KKnown -> soft_fail(program, ctx, pc)
        _ -> Advance(ctx)
      }
  }
}

/// `no_readers(X)` (Dart runner.dart:3918): readers present → SUSPEND on them,
/// never fails; none → SUCCEED. Negation inverts.
fn guard_no_readers(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  var_index: Int,
  negated: Bool,
) -> Step {
  case dict.get(ctx.clause_vars, var_index) {
    Error(_) ->
      case negated {
        True -> soft_fail(program, ctx, pc)
        False -> Advance(ctx)
      }
    Ok(cvar) -> {
      let c = collect_cvar(ctx, cvar, empty_collect())
      case set.is_empty(c.readers), negated {
        True, False -> Advance(ctx)
        False, False -> guard_suspend(program, ctx, pc, c.readers)
        False, True -> Advance(ctx)
        True, True -> soft_fail(program, ctx, pc)
      }
    }
  }
}

/// `X =?= Y` (Dart runner.dart:4049): unbound writer → FAIL, unbound readers →
/// SUSPEND, else compare ground values. Negation inverts equality.
fn guard_ground_equal(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  left: Int,
  right: Int,
  negated: Bool,
) -> Step {
  case dict.get(ctx.clause_vars, left), dict.get(ctx.clause_vars, right) {
    Ok(lc), Ok(rc) -> {
      let c = collect_cvar(ctx, rc, collect_cvar(ctx, lc, empty_collect()))
      case c.has_writer, set.is_empty(c.readers) {
        True, _ -> soft_fail(program, ctx, pc)
        False, False -> guard_suspend(program, ctx, pc, c.readers)
        False, True -> {
          let eq = resolve_cvar(ctx, lc) == resolve_cvar(ctx, rc)
          case eq == !negated {
            True -> Advance(ctx)
            False -> soft_fail(program, ctx, pc)
          }
        }
      }
    }
    _, _ -> soft_fail(program, ctx, pc)
  }
}

fn guard_suspend(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  writers: Set(Int),
) -> Step {
  soft_fail_with(
    program,
    RunnerContext(..ctx, u: set.union(ctx.u, writers)),
    pc,
  )
}

// Fully resolve a value to a ground term for `=?=` comparison (σ̂w + heap).
fn resolve_cvar(ctx: RunnerContext, cvar: CVar) -> Term {
  case cvar {
    CVTerm(t) -> resolve_term(ctx, t)
    CVAddr(a) -> resolve_addr(ctx, a)
    CVTentative(tent) -> resolve_tent(ctx, tent)
    CVState(_, _, _, _) -> ConstTerm(ConstAtom("$state"))
  }
}

fn resolve_term(ctx: RunnerContext, term: Term) -> Term {
  case term {
    VarRef(addr) -> resolve_addr(ctx, addr)
    StructTerm(f, args) ->
      StructTerm(f, list.map(args, fn(a) { resolve_term(ctx, a) }))
    ConstTerm(_) -> term
  }
}

fn resolve_addr(ctx: RunnerContext, addr: Int) -> Term {
  case dict.get(ctx.sigma_hat, addr) {
    Ok(SVTerm(t)) -> resolve_term(ctx, t)
    Ok(SVTentative(tent)) -> resolve_tent(ctx, tent)
    Error(_) ->
      case dval(ctx.heap, addr) {
        Bound(v) -> resolve_term(ctx, v)
        Unbound(w) -> VarRef(w)
      }
  }
}

fn resolve_tent(ctx: RunnerContext, tent: TentStruct) -> Term {
  StructTerm(
    tent.functor,
    list.map(tent.args, fn(slot) {
      case slot {
        TSTerm(t) -> resolve_term(ctx, t)
        TSNested(n) -> resolve_tent(ctx, n)
        TSVoid -> ConstTerm(ConstAtom("$void"))
        TSClauseVar(vi, _) ->
          case dict.get(ctx.clause_vars, vi) {
            Ok(cv) -> resolve_cvar(ctx, cv)
            Error(_) -> ConstTerm(ConstAtom("$unresolved"))
          }
      }
    }),
  )
}

// ── GUARD phase: the generic `Guard` opcode (T024) ───────────────────────────
//
// The codegen routes every non-structural guard — arithmetic comparisons
// (`< > =< >= =:= =\=`), standard-order term comparators (`@< @> @=< @>=`), type
// tests (`integer/atom/string/constant/number/list/compound/...`), `=?=`, and
// `unknown` — to `Guard(predicate, arity, negated)`, after emitting a `put_arg`
// per operand (Dart `generic_guard`; the operands land in `arg_slots`). This is
// the general Guard handler (Dart runner.dart:3503 `if (op is Guard)`):
//   1. resolve each `arg_slots[i]` (σ̂w + heap, deep) collecting nested unbound
//      READER terminals for suspension — Dart `_dereferenceWithTracking` +
//      `_collectUnboundReaders`;
//   2. any unbound reader (and predicate ≠ `unknown`) → SUSPEND on it;
//   3. else evaluate the predicate over the resolved (ground) operands
//      (Dart `_evaluateGuard`); NEGATION inverts success↔failure, suspend
//      unchanged (Dart runner.dart:3628).
// Semantics frozen (Constitution IV-a / Language Authority §1.14) — an unknown
// predicate FAILS as in the Dart `default` arm (`[WARN]`), and the effectful
// `wait`/`wait_until` timer guards are surfaced as `Unimplemented` (they need
// the scheduler's timer infrastructure, out of the pure-engine MVP; escalate if
// the corpus hits them rather than invent wall-clock semantics). Runtime-defined
// guards (049, Dart runner.dart:461-764) are out of T024 scope: a user guard
// predicate not in the builtin set takes the unknown→FAIL arm.

/// The two-valued outcome of evaluating a guard predicate over ground operands
/// (suspension is decided upstream, in the deref pass). `GUnsupported` marks an
/// effectful/unported predicate that must be surfaced, never guessed.
type GuardVerdict {
  GSuccess
  GFailure
  GUnsupported(name: String)
}

fn guard_generic(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
  predicate: String,
  arity: Int,
  negated: Bool,
) -> Step {
  let #(args, c) = guard_gather(ctx, arity)
  case !set.is_empty(c.readers) && predicate != "unknown" {
    True -> guard_suspend(program, ctx, pc, c.readers)
    False ->
      case eval_guard(predicate, args) {
        GUnsupported(name) -> Stop(RunnerError(Unimplemented("guard " <> name)))
        verdict -> {
          let succeeded = case verdict {
            GSuccess -> True
            _ -> False
          }
          case succeeded == !negated {
            True -> Advance(ctx)
            False -> soft_fail(program, ctx, pc)
          }
        }
      }
  }
}

/// Gather `arg_slots[0..arity-1]` (Dart falls back to `clauseVars[i]`), deeply
/// resolving each through σ̂w + heap and accumulating unbound-reader terminals
/// (the suspension set) via the shared cycle-safe walker.
fn guard_gather(ctx: RunnerContext, arity: Int) -> #(List(Term), Collect) {
  list.fold(upto(arity), #([], empty_collect()), fn(acc, i) {
    let #(args, c) = acc
      let arg = case dict.get(ctx.arg_slots, i) {
      Ok(t) -> t
      Error(_) ->
        case dict.get(ctx.clause_vars, i) {
          Ok(cv) -> resolve_cvar(ctx, cv)
          Error(_) -> ConstTerm(ConstAtom("$missing"))
        }
    }
    let #(resolved, c2) = g_walk(ctx, arg, c, set.new())
    #(list.append(args, [resolved]), c2)
  })
}

/// Deep-resolve `term` through σ̂w + heap into a ground term, collecting unbound
/// READER terminals (→ suspend) and noting an unbound writer (→ comparator
/// FAILs, per Dart's verdict matrix). Mirrors `_dereferenceWithTracking`'s
/// reader→paired-writer→σ̂w lookup by consulting σ̂w on the deref TERMINAL writer.
fn g_walk(
  ctx: RunnerContext,
  term: Term,
  c: Collect,
  visited: Set(Int),
) -> #(Term, Collect) {
  case term {
    ConstTerm(_) -> #(term, c)
    StructTerm(functor, args) -> {
      let #(rev, c2) =
        list.fold(args, #([], c), fn(a, arg) {
          let #(acc, cc) = a
          let #(r, cc2) = g_walk(ctx, arg, cc, visited)
          #([r, ..acc], cc2)
        })
      #(StructTerm(functor, list.reverse(rev)), c2)
    }
    VarRef(addr) -> g_walk_addr(ctx, addr, c, visited)
  }
}

fn g_walk_addr(
  ctx: RunnerContext,
  addr: Int,
  c: Collect,
  visited: Set(Int),
) -> #(Term, Collect) {
  case set.contains(visited, addr) {
    True -> #(VarRef(addr), c)
    False -> {
      let visited = set.insert(visited, addr)
      case dval(ctx.heap, addr) {
        Bound(v) -> g_walk(ctx, v, c, visited)
        Unbound(terminal) ->
          case dict.get(ctx.sigma_hat, terminal) {
            Ok(SVTerm(t)) -> g_walk(ctx, t, c, visited)
            Ok(SVTentative(tent)) ->
              g_walk(ctx, resolve_tent(ctx, tent), c, visited)
            Error(_) ->
              case terminal == addr {
                // unbound WRITER (deref terminal is itself): comparator FAILs.
                True -> #(VarRef(terminal), Collect(..c, has_writer: True))
                // unbound READER chain ending at `terminal`: suspend on it.
                False -> #(
                  VarRef(terminal),
                  Collect(..c, readers: set.insert(c.readers, terminal)),
                )
              }
          }
      }
    }
  }
}

/// Evaluate a guard predicate over already-resolved ground operands (Dart
/// `_evaluateGuard`). An unbound writer arrives as a bare `VarRef`.
fn eval_guard(predicate: String, args: List(Term)) -> GuardVerdict {
  case predicate {
    "<" -> num_cmp(args, fn(o) { o == order.Lt })
    ">" -> num_cmp(args, fn(o) { o == order.Gt })
    "=<" -> num_cmp(args, fn(o) { o != order.Gt })
    ">=" -> num_cmp(args, fn(o) { o != order.Lt })
    "=:=" -> num_cmp(args, fn(o) { o == order.Eq })
    "=\\=" -> num_cmp(args, fn(o) { o != order.Eq })
    "@<" -> term_cmp(args, fn(r) { r < 0 })
    "@>" -> term_cmp(args, fn(r) { r > 0 })
    "@=<" -> term_cmp(args, fn(r) { r <= 0 })
    "@>=" -> term_cmp(args, fn(r) { r >= 0 })
    "=?=" ->
      case args {
        [a, b] ->
          case is_unbound(a) || is_unbound(b) {
            True -> GFailure
            False -> verdict(compare_terms(a, b) == 0)
          }
        _ -> GFailure
      }
    "integer" -> type_test(args, is_integer)
    "number" -> type_test(args, is_number)
    "atom" | "string" -> type_test(args, is_string_atom)
    "constant" -> type_test(args, is_constant)
    "list" | "is_list" -> type_test(args, is_list_term)
    "compound" | "tuple" -> type_test(args, is_compound)
    "unknown" -> type_test(args, is_unbound)
    // `known`/`ground` are normally specialized (Known/Ground opcodes); the
    // generic arms mirror Dart's `_evaluateGuard` for the fallback path.
    "known" -> type_test(args, fn(t) { !is_unbound(t) })
    "ground" -> GSuccess
    "otherwise" -> GSuccess
    // Effectful timer guards — need scheduler timer infra (out of pure-engine
    // MVP). Surface, do not invent (Language Authority §1.14).
    "wait" | "wait_until" -> GUnsupported(predicate)
    // Unknown predicate → FAIL (Dart `_evaluateGuard` default `[WARN]` arm).
    // Also the fall-through for unported runtime-defined guards (049).
    _ -> GFailure
  }
}

fn verdict(b: Bool) -> GuardVerdict {
  case b {
    True -> GSuccess
    False -> GFailure
  }
}

/// `[0, 1, …, n-1]` (`gleam/list` has no `range` in this stdlib version).
fn upto(n: Int) -> List(Int) {
  upto_loop(n - 1, [])
}

fn upto_loop(i: Int, acc: List(Int)) -> List(Int) {
  case i < 0 {
    True -> acc
    False -> upto_loop(i - 1, [i, ..acc])
  }
}

fn type_test(args: List(Term), pred: fn(Term) -> Bool) -> GuardVerdict {
  case args {
    [a, ..] -> verdict(pred(a))
    [] -> GFailure
  }
}

/// Arithmetic comparison: evaluate both operands numerically; unless BOTH are
/// numeric, FAIL (Dart returns failure when either `evaluateNumeric` is null).
fn num_cmp(args: List(Term), ok: fn(order.Order) -> Bool) -> GuardVerdict {
  case args {
    [a, b, ..] ->
      case evaluate_numeric(a), evaluate_numeric(b) {
        Ok(na), Ok(nb) -> verdict(ok(arith.compare(na, nb)))
        _, _ -> GFailure
      }
    _ -> GFailure
  }
}

fn term_cmp(args: List(Term), ok: fn(Int) -> Bool) -> GuardVerdict {
  case args {
    [a, b, ..] -> verdict(ok(compare_terms(a, b)))
    _ -> GFailure
  }
}

/// Evaluate a fully-resolved term numerically (Dart `evaluateNumeric` over the
/// already-dereferenced operand): a numeric constant, or an arithmetic
/// StructTerm (`+ - * / // mod neg`) combined via the shared `arith` core. An
/// unbound writer (`VarRef`) or non-numeric leaf → not numeric.
fn evaluate_numeric(term: Term) -> Result(NumV, Nil) {
  case term {
    ConstTerm(ConstInt(i)) -> Ok(NInt(i))
    ConstTerm(ConstReal(f)) -> Ok(NReal(f))
    ConstTerm(_) -> Error(Nil)
    VarRef(_) -> Error(Nil)
    StructTerm(functor, args) ->
      case result.all(list.map(args, evaluate_numeric)) {
        Ok(nums) -> arith.combine(functor, nums)
        Error(_) -> Error(Nil)
      }
  }
}

// ── type-test predicates (Dart `_evaluateGuard` getValue-based arms) ──────────

fn is_unbound(t: Term) -> Bool {
  case t {
    VarRef(_) -> True
    _ -> False
  }
}

fn is_integer(t: Term) -> Bool {
  case t {
    ConstTerm(ConstInt(_)) -> True
    _ -> False
  }
}

fn is_number(t: Term) -> Bool {
  case t {
    ConstTerm(ConstInt(_)) | ConstTerm(ConstReal(_)) -> True
    _ -> False
  }
}

/// `atom`/`string` — a String-valued constant that is not `nil` (Dart erases
/// atoms and strings to `String`; both pass, `[]`/`nil` excluded).
fn is_string_atom(t: Term) -> Bool {
  case t {
    ConstTerm(ConstString(_)) -> True
    ConstTerm(ConstAtom(a)) -> a != "nil"
    _ -> False
  }
}

/// `constant` — any ground constant (Dart: String incl. `nil`, or a number).
fn is_constant(t: Term) -> Bool {
  case t {
    ConstTerm(_) -> True
    _ -> False
  }
}

fn is_list_term(t: Term) -> Bool {
  case t {
    ConstTerm(ConstAtom("nil")) -> True
    StructTerm(".", [_, _]) -> True
    _ -> False
  }
}

fn is_compound(t: Term) -> Bool {
  case t {
    StructTerm(_, [_, ..]) -> True
    _ -> False
  }
}

// ── standard order of terms (Dart `_orderRank` / `_compareTerms`) ─────────────
//
// Total order Number < String(atom) < compound; within numbers by value, within
// strings by code-point, within compounds by arity, then functor, then args.
// MUST stay behaviour-identical to the C# port (FR-060). Operands are ground.

fn order_rank(t: Term) -> Int {
  case t {
    ConstTerm(ConstInt(_)) | ConstTerm(ConstReal(_)) -> 0
    ConstTerm(ConstAtom(_)) | ConstTerm(ConstString(_)) -> 1
    StructTerm(_, _) -> 2
    VarRef(_) -> 3
  }
}

fn compare_terms(a: Term, b: Term) -> Int {
  let ra = order_rank(a)
  let rb = order_rank(b)
  case ra == rb {
    False ->
      case ra < rb {
        True -> -1
        False -> 1
      }
    True ->
      case ra {
        0 ->
          case evaluate_numeric(a), evaluate_numeric(b) {
            Ok(na), Ok(nb) -> order_to_int(arith.compare(na, nb))
            _, _ -> 0
          }
        1 -> order_to_int(string.compare(const_string(a), const_string(b)))
        2 -> compare_structs(a, b)
        _ -> order_to_int(string.compare(string.inspect(a), string.inspect(b)))
      }
  }
}

fn compare_structs(a: Term, b: Term) -> Int {
  case a, b {
    StructTerm(fa, aargs), StructTerm(fb, bargs) -> {
      let la = list.length(aargs)
      let lb = list.length(bargs)
      case la == lb {
        False ->
          case la < lb {
            True -> -1
            False -> 1
          }
        True ->
          case order_to_int(string.compare(fa, fb)) {
            0 -> compare_args(aargs, bargs)
            fc -> fc
          }
      }
    }
    _, _ -> 0
  }
}

fn compare_args(a: List(Term), b: List(Term)) -> Int {
  case a, b {
    [], [] -> 0
    [x, ..xs], [y, ..ys] ->
      case compare_terms(x, y) {
        0 -> compare_args(xs, ys)
        c -> c
      }
    _, _ -> 0
  }
}

fn const_string(t: Term) -> String {
  case t {
    ConstTerm(ConstAtom(s)) -> s
    ConstTerm(ConstString(s)) -> s
    _ -> ""
  }
}

fn order_to_int(o: order.Order) -> Int {
  case o {
    order.Lt -> -1
    order.Eq -> 0
    order.Gt -> 1
  }
}

// ── Commit (Dart runner.dart:2703) ───────────────────────────────────────────
//
// Two-phase Si resolution, convert tentative structures → StructTerms (resolving
// clause-var placeholders), then apply σ̂w to the heap under the writer-MGU
// discipline. A still-unresolved Si soft-fails; a clean commit reduces.

fn commit(program: BytecodeProgram, ctx: RunnerContext, pc: Int) -> Step {
  let unresolved =
    set.filter(ctx.si, fn(writer) {
      !dict.has_key(ctx.sigma_hat, writer) && !heap.is_value(ctx.heap, writer)
    })
  case set.is_empty(unresolved) {
    False ->
      soft_fail_with(
        program,
        RunnerContext(..ctx, u: set.union(ctx.u, unresolved)),
        pc,
      )
    True ->
      case convert_sigma_hat(ctx, dict.to_list(ctx.sigma_hat), []) {
        Error(fault) -> Stop(RunnerError(fault))
        Ok(#(ctx, converted)) ->
          case apply_sigma_hat(ctx.heap, converted, []) {
            Error(fault) -> Stop(RunnerError(fault))
            Ok(#(heap, woken)) ->
              // σ̂w applied; enter BODY (this slice's BODY is Proceed-only, so
              // the woken reactivations ride out on the reduction outcome).
              Advance(
                RunnerContext(
                  ..ctx,
                  heap: heap,
                  sigma_hat: dict.new(),
                  si: set.new(),
                  current: BTNone,
                  current_writer: None,
                  mode: ReadMode,
                  s: 0,
                  arg_slots: dict.new(),
                  parent_stack: [],
                  build_writer: None,
                  build_slot: None,
                  in_body: True,
                  woken: list.append(ctx.woken, woken),
                ),
              )
          }
      }
  }
}

/// Convert every σ̂w entry to a `#(writer, Term)`, materializing tentative
/// structures (recursively) and resolving clause-var placeholders. Threads the
/// context because placeholder resolution may allocate fresh heap variables.
fn convert_sigma_hat(
  ctx: RunnerContext,
  entries: List(#(Int, SigmaVal)),
  acc: List(#(Int, Term)),
) -> Result(#(RunnerContext, List(#(Int, Term))), RunnerFault) {
  case entries {
    [] -> Ok(#(ctx, list.reverse(acc)))
    [#(writer, SVTerm(term)), ..rest] ->
      convert_sigma_hat(ctx, rest, [#(writer, term), ..acc])
    [#(writer, SVTentative(tent)), ..rest] ->
      case convert_tentative(ctx, tent) {
        Error(fault) -> Error(fault)
        Ok(#(ctx, term)) ->
          convert_sigma_hat(ctx, rest, [#(writer, term), ..acc])
      }
  }
}

/// Recursively materialize a tentative structure into a `StructTerm` (Dart
/// Commit conversion + `_convertTentativeToStruct`).
fn convert_tentative(
  ctx: RunnerContext,
  tent: TentStruct,
) -> Result(#(RunnerContext, Term), RunnerFault) {
  case convert_slots(ctx, tent.args, []) {
    Error(fault) -> Error(fault)
    Ok(#(ctx, args)) -> Ok(#(ctx, StructTerm(tent.functor, args)))
  }
}

fn convert_slots(
  ctx: RunnerContext,
  slots: List(TentSlot),
  acc: List(Term),
) -> Result(#(RunnerContext, List(Term)), RunnerFault) {
  case slots {
    [] -> Ok(#(ctx, list.reverse(acc)))
    [slot, ..rest] ->
      case convert_slot(ctx, slot) {
        Error(fault) -> Error(fault)
        Ok(#(ctx, term)) -> convert_slots(ctx, rest, [term, ..acc])
      }
  }
}

fn convert_slot(
  ctx: RunnerContext,
  slot: TentSlot,
) -> Result(#(RunnerContext, Term), RunnerFault) {
  case slot {
    TSTerm(term) -> Ok(#(ctx, term))
    TSNested(nested) -> convert_tentative(ctx, nested)
    // Dart maps a void slot to `ConstTerm(null)`, which the Gleam constant model
    // cannot represent. This path is reachable only via a WRITE-mode anonymous
    // slot in an output HEAD structure; surface it rather than guess a value.
    TSVoid ->
      Error(StructuralViolation(
        "void slot in a committed HEAD structure has no Gleam representation "
        <> "(Dart ConstTerm(null)) — escalate (frozen-semantics gap)",
      ))
    TSClauseVar(var_index, is_writer) ->
      convert_clausevar(ctx, var_index, is_writer)
  }
}

/// Resolve a clause-var placeholder at Commit (Dart runner.dart:2747-2787).
fn convert_clausevar(
  ctx: RunnerContext,
  var_index: Int,
  is_writer: Bool,
) -> Result(#(RunnerContext, Term), RunnerFault) {
  case dict.get(ctx.clause_vars, var_index) {
    Ok(CVTerm(VarRef(addr))) -> {
      let resolved_writer = heap.is_writer(ctx.heap, addr)
      let term = case is_writer, resolved_writer {
        True, True -> VarRef(addr)
        True, False ->
          case heap.paired_writer(ctx.heap, addr) {
            Ok(w) -> VarRef(w)
            Error(_) -> VarRef(addr)
          }
        False, False -> VarRef(addr)
        False, True -> VarRef(heap.paired_reader(ctx.heap, addr))
      }
      Ok(#(ctx, term))
    }
    Ok(CVTerm(term)) -> Ok(#(ctx, term))
    _ -> {
      let #(heap, w, r) = heap.allocate_variable(ctx.heap)
      let addr = case is_writer {
        True -> w
        False -> r
      }
      let ctx =
        RunnerContext(
          ..ctx,
          heap: heap,
          clause_vars: dict.insert(
            ctx.clause_vars,
            var_index,
            CVTerm(VarRef(addr)),
          ),
        )
      Ok(#(ctx, VarRef(addr)))
    }
  }
}

/// Apply σ̂w to the heap: bind each writer to its value (writer→value) or onward
/// to a reader (writer→var). WxW / double-bind surface as a fault. Collects the
/// reactivations the bindings wake.
fn apply_sigma_hat(
  heap: Heap,
  bindings: List(#(Int, Term)),
  woken: List(GoalRef),
) -> Result(#(Heap, List(GoalRef)), RunnerFault) {
  case bindings {
    [] -> Ok(#(heap, woken))
    [#(writer, value), ..rest] -> {
      let bound = case value {
        VarRef(reader) -> heap.bind_writer_to_var(heap, writer, reader)
        _ -> heap.bind_writer(heap, writer, value)
      }
      case bound {
        Error(e) -> Error(StructuralViolation(heap_error_detail(e)))
        Ok(#(heap, fired)) ->
          apply_sigma_hat(heap, rest, list.append(woken, fired))
      }
    }
  }
}

// ── NoMoreClauses (Dart runner.dart:2875) ────────────────────────────────────

fn no_more_clauses(ctx: RunnerContext) -> ReduceOutcome {
  let on = set.filter(ctx.u, fn(writer) { heap.is_writer(ctx.heap, writer) })
  case set.is_empty(on) {
    True -> Failed(ctx.heap)
    False -> Suspended(ctx.heap, on)
  }
}

// ── Soft-fail to the next clause (Dart _softFailToNextClause + _findNextClauseTry) ──

fn soft_fail(program: BytecodeProgram, ctx: RunnerContext, pc: Int) -> Step {
  soft_fail_with(program, RunnerContext(..ctx, u: set.union(ctx.u, ctx.si)), pc)
}

fn soft_fail_with(
  program: BytecodeProgram,
  ctx: RunnerContext,
  pc: Int,
) -> Step {
  let ctx = clear_clause(ctx)
  case find_next_clause_try(program, pc + 1) {
    Ok(target) -> Jump(ctx, target)
    Error(_) ->
      Stop(
        RunnerError(Malformed(
          "soft-fail: no clause boundary after pc " <> int.to_string(pc),
        )),
      )
  }
}

/// Scan forward for the next clause boundary (Dart `_findNextClauseTry`).
fn find_next_clause_try(program: BytecodeProgram, pc: Int) -> Result(Int, Nil) {
  case program.op_at(program, pc) {
    Error(_) -> Error(Nil)
    Ok(op) ->
      case op {
        opcodes.ClauseTry | opcodes.ClauseNext(_) | opcodes.NoMoreClauses ->
          Ok(pc)
        _ -> find_next_clause_try(program, pc + 1)
      }
  }
}

// ── Clause reset (Dart RunnerContext.clearClause — U is preserved) ──────────

fn clear_clause(ctx: RunnerContext) -> RunnerContext {
  RunnerContext(
    ..ctx,
    sigma_hat: dict.new(),
    si: set.new(),
    clause_vars: dict.new(),
    mode: ReadMode,
    s: 0,
    current: BTNone,
    current_writer: None,
    arg_slots: dict.new(),
    build_writer: None,
    build_slot: None,
    parent_stack: [],
    in_body: False,
  )
}

// ── Small mutation helpers ───────────────────────────────────────────────────

fn set_cvar(ctx: RunnerContext, var_index: Int, value: CVar) -> RunnerContext {
  RunnerContext(
    ..ctx,
    clause_vars: dict.insert(ctx.clause_vars, var_index, value),
  )
}

fn bind_sigma(ctx: RunnerContext, writer: Int, value: Term) -> RunnerContext {
  RunnerContext(
    ..ctx,
    sigma_hat: dict.insert(ctx.sigma_hat, writer, SVTerm(value)),
  )
}

fn bind_sigma_val(
  ctx: RunnerContext,
  writer: Int,
  value: SigmaVal,
) -> RunnerContext {
  RunnerContext(..ctx, sigma_hat: dict.insert(ctx.sigma_hat, writer, value))
}

fn advance_s(ctx: RunnerContext) -> RunnerContext {
  RunnerContext(..ctx, s: ctx.s + 1)
}

/// Set `current`, keeping `sigma_hat[current_writer]` in sync with the
/// top-level tentative (reproduces the Dart shared-reference behaviour).
fn put_current(ctx: RunnerContext, target: BuildTarget) -> RunnerContext {
  let sigma_hat = case ctx.current_writer, target {
    Some(wid), BTTentative(t) -> dict.insert(ctx.sigma_hat, wid, SVTentative(t))
    _, _ -> ctx.sigma_hat
  }
  RunnerContext(..ctx, current: target, sigma_hat: sigma_hat)
}

// ── Term equality by shape (Dart GetValue match, runner.dart:2394) ───────────

fn terms_shape_equal(a: Term, b: Term) -> Bool {
  case a, b {
    ConstTerm(x), ConstTerm(y) -> x == y
    StructTerm(fx, ax), StructTerm(fy, ay) ->
      fx == fy && list.length(ax) == list.length(ay)
    _, _ -> a == b
  }
}

// ── List index helpers (immutable structure args) ────────────────────────────

fn list_at(xs: List(a), i: Int) -> Result(a, Nil) {
  case xs, i {
    [], _ -> Error(Nil)
    [x, ..], 0 -> Ok(x)
    [_, ..rest], _ -> list_at(rest, i - 1)
  }
}

fn list_set(xs: List(a), i: Int, value: a) -> List(a) {
  case xs, i {
    [], _ -> []
    [_, ..rest], 0 -> [value, ..rest]
    [x, ..rest], _ -> [x, ..list_set(rest, i - 1, value)]
  }
}

fn heap_error_detail(e: HeapError) -> String {
  string.inspect(e)
}
