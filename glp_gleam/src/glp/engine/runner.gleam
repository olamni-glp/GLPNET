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
//// This slice (21a/b) implements the control-flow spine + the HEAD-constant
//// path + Commit + suspension, enough to run ground constant-headed procedures
//// end-to-end. The HEAD-structure machinery (`_TentativeStruct`, S/mode,
//// head_structure/head_list/unify_*), GetVariable/GetValue clause-variable
//// unification, GUARD evaluation, and BODY put_*/spawn are the next slices
//// (21c/21d → T023/T024). Any opcode not yet ported returns
//// `RunnerError(Unimplemented(..))` — surfaced, never silently skipped.
////
//// Suspension model adaptation (faithful, documented): the Dart keeps reader
//// addresses in Si/U and maps to paired writers at suspend time; the Gleam
//// foundation reactivates on WRITER binding (heap.suspend_on_writer /
//// bind_writer), and `deref` of an unbound reader already yields its terminal
//// writer — so Si/U and `Suspended(on:)` carry WRITER addresses, which the
//// scheduler (T022) registers directly against the suspension table.

import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import gleam/set.{type Set}
import gleam/string
import glp/bytecode/opcodes.{type Op}
import glp/bytecode/program.{type BytecodeProgram, type XRegs}
import glp/runtime/heap.{type Heap, type HeapError, Bound, Unbound}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{type Constant, type Term, ConstAtom, ConstTerm, VarRef}

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

/// Per-reduction execution context (Dart `RunnerContext`, the fields this slice
/// uses). Immutable — every handler returns a new context.
pub type RunnerContext {
  RunnerContext(
    heap: Heap,
    /// This goal's argument registers A0..An-1 (call args as terms).
    regs: XRegs,
    /// σ̂w — tentative writer bindings (writer addr → value) built in HEAD,
    /// applied to the heap atomically at Commit.
    sigma_hat: Dict(Int, Term),
    /// Per-clause preliminary suspension set (writer addresses whose binding
    /// would resolve a HEAD indeterminacy in the current clause).
    si: Set(Int),
    /// Goal-level suspension set (writer addresses), accumulated across clause
    /// attempts; consumed by NoMoreClauses. Survives clause clears.
    u: Set(Int),
    /// Goals to enqueue once the reduction succeeds (commit-woken now; BODY
    /// spawns later).
    spawned: List(GoalRef),
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

    // ── HEAD phase (constant subset — this slice) ───────────────────────────
    opcodes.HeadConstant(value, arg_slot) ->
      head_match_constant(program, ctx, pc, value, arg_slot)
    opcodes.HeadNil(arg_slot) ->
      head_match_constant(program, ctx, pc, ConstAtom("nil"), arg_slot)

    // ── Not yet ported (surfaced, never silently skipped) ───────────────────
    _ -> Stop(RunnerError(Unimplemented(opcodes.mnemonic(op))))
  }
}

// ── HEAD constant matching (Dart HeadConstant, runner.dart:1126) ─────────────
//
// Resolve the argument at `arg_slot`, then: a bound value → compare (mismatch
// soft-fails); an unbound writer → record `σ̂w[writer] = const` (or, if already
// tentatively bound, compare); an unbound reader → add its terminal writer to
// Si (two-phase: re-checked at Commit) and continue.

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
        // Ground value already present — structural equality.
        ArgValue(ConstTerm(c)) ->
          case c == value {
            True -> Advance(ctx)
            False -> soft_fail(program, ctx, pc)
          }
        ArgValue(_) -> soft_fail(program, ctx, pc)
        // Unbound writer: tentatively bind in σ̂w, or compare if already bound
        // there this clause.
        ArgWriter(writer) ->
          case dict.get(ctx.sigma_hat, writer) {
            Ok(ConstTerm(c)) ->
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
                    ConstTerm(value),
                  ),
                ),
              )
          }
        // Unbound reader: indeterminate — defer via Si on the paired writer.
        ArgReader(paired_writer) ->
          Advance(RunnerContext(..ctx, si: set.insert(ctx.si, paired_writer)))
      }
  }
}

/// The resolved role of an argument term (like `unify.resolve`, but returning
/// the writer/reader distinction the runner needs). Writer/reader carry the
/// terminal writer address.
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

// ── Commit (Dart runner.dart:2703) ───────────────────────────────────────────
//
// Two-phase Si resolution, then apply σ̂w to the heap (writer-MGU: bind writers
// only). A still-unresolved Si soft-fails the clause; a clean commit reduces.

fn commit(program: BytecodeProgram, ctx: RunnerContext, pc: Int) -> Step {
  // An Si entry (a writer address) is resolved once it is bound — in σ̂w now, or
  // already a value in the heap.
  let unresolved =
    set.filter(ctx.si, fn(writer) {
      !dict.has_key(ctx.sigma_hat, writer) && !heap.is_value(ctx.heap, writer)
    })
  case set.is_empty(unresolved) {
    // Clause cannot commit: its HEAD is still indeterminate — defer to
    // suspension by moving the unresolved writers to U and trying the next
    // clause.
    False ->
      soft_fail_with(
        program,
        RunnerContext(..ctx, u: set.union(ctx.u, unresolved)),
        pc,
      )
    True ->
      case apply_sigma_hat(ctx.heap, dict.to_list(ctx.sigma_hat), []) {
        Error(fault) -> Stop(RunnerError(fault))
        Ok(#(heap, woken)) ->
          // σ̂w applied; enter BODY. This slice's BODY is Proceed-only, so the
          // commit-woken reactivations ride out on the reduction outcome.
          Advance(
            RunnerContext(
              ..ctx,
              heap: heap,
              sigma_hat: dict.new(),
              si: set.new(),
              spawned: list.append(ctx.spawned, woken),
            ),
          )
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
  // Suspend only on addresses that are still unbound writers (a bound entry can
  // no longer be waited on — writer-keyed suspension).
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
  RunnerContext(..ctx, sigma_hat: dict.new(), si: set.new())
}

fn heap_error_detail(e: HeapError) -> String {
  string.inspect(e)
}
