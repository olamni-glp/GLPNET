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
import gleam/set.{type Set}
import gleam/string
import glp/bytecode/opcodes.{type Op}
import glp/bytecode/program.{type BytecodeProgram, type XRegs}
import glp/runtime/heap.{type Heap, type HeapError, Bound, Unbound}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{
  type Constant, type Term, ConstAtom, ConstTerm, StructTerm, VarRef,
}

/// The outcome of one goal reduction, handed back to the scheduler (T022).
pub type ReduceOutcome {
  /// A clause committed: the goal reduced. `heap` is post-commit; `spawned`
  /// carries goals to enqueue — reactivations woken by the commit binding plus
  /// (once BODY lands) body-spawned goals.
  Reduced(heap: Heap, spawned: List(GoalRef))
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
/// `cx.currentStructure`.
pub type BuildTarget {
  BTNone
  /// READ-mode traversal over a real structure's args.
  BTStruct(functor: String, args: List(Term))
  /// WRITE-mode HEAD building.
  BTTentative(TentStruct)
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
    /// Goals to enqueue once the reduction succeeds.
    spawned: List(GoalRef),
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
    /// Phase flag: `False` = HEAD/GUARD, `True` = BODY (set at Commit).
    in_body: Bool,
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
    spawned: [],
    clause_vars: dict.new(),
    mode: ReadMode,
    s: 0,
    current: BTNone,
    current_writer: None,
    in_body: False,
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
    opcodes.Proceed -> Stop(Reduced(ctx.heap, ctx.spawned))
    opcodes.Halt -> Stop(Reduced(ctx.heap, ctx.spawned))

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
    // WRITE into a real StructTerm is BODY construction (slice 21d).
    WriteMode, _ -> Stop(RunnerError(Unimplemented("unify_variable (body)")))
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
    WriteMode, _ -> Stop(RunnerError(Unimplemented("unify_constant (body)")))
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
  // Prefer a σ̂w tentative binding, then the heap (Dart runner.dart:942).
  let value = case raw {
    VarRef(addr) ->
      case dict.get(ctx.sigma_hat, addr) {
        Ok(SVTerm(v)) -> v
        Ok(SVTentative(_)) -> raw
        Error(_) -> deref_value(ctx, raw)
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
                  in_body: True,
                  spawned: list.append(ctx.spawned, woken),
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
