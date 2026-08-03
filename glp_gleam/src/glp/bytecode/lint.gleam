//// glp/bytecode/lint — v2.16 instruction-stream well-formedness checks
//// (feature 064, T034 — completes the recorded `glp/lint` placeholder for the
//// bytecode subsystem).
////
//// Normative spec: docs/glp-bytecode-v216-complete.md —
////   * §4 Environment and Phase Discipline: head/guard opcodes never allocate
////     or deallocate frames; `allocate`/`deallocate` (and the other body-side
////     control ops) belong to BODY only.
////   * §7 Body Construction: put-family ops are BODY heap builders, with the
////     documented EXCEPTION that put-family register loads are legal in the
////     HEAD/GUARD phase for guard argument setup (the shape the production
////     codegen emits — glp/compiler/codegen.gleam `generic_guard`).
////   * §2/§3/§9: clause bracketing — `clause_try` opens a clause, `commit`
////     enters BODY, `proceed` (or `clause_next`/`try_next_clause` pre-commit)
////     closes it, `no_more_clauses` ends the procedure at predicate level.
////
//// Dart lineage: glp_runtime/lib/lint/linter.dart (LintIssue/LintResult and
//// the clause-bracketing state machine). The Dart checks are written against
//// the pre-v2.16 opcode inventory (BodySet*/SuspendEnd/HeadBindWriter — the
//// forms deliberately NOT ported, see glp/bytecode/opcodes.gleam header); this
//// module re-expresses the same discipline over the ported v2.16 union.
////
//// The Gleam `Op` union is closed, so a malformed opcode ID or a wrong operand
//// COUNT is unrepresentable by construction; their lintable analogues are
//// operand VALUES no instruction is defined for (negative register/argument
//// indexes, zero-length voids, empty functors), a `Spawn`/`Requeue` whose
//// "name/arity" label disagrees with its arity operand, and references that
//// resolve to nothing (a `clause_next` target absent from the label table, a
//// `distribute` import index absent from the import table).
////
//// Pure and read-only: linting never mutates the program and has no effect on
//// engine dispatch or semantics.

import gleam/int
import gleam/list
import gleam/string
import glp/bytecode/opcodes.{type Op}
import glp/bytecode/program.{type BytecodeProgram}

// ============================================================================
// Result model (Dart LintIssue / LintResult)
// ============================================================================

/// One well-formedness finding: a stable issue code, a human message, and the
/// PC (op index) it was found at.
pub type LintIssue {
  LintIssue(code: String, message: String, pc: Int)
}

/// All findings for one program, in PC order.
pub type LintResult {
  LintResult(issues: List(LintIssue))
}

/// True iff the program linted clean.
pub fn is_ok(result: LintResult) -> Bool {
  result.issues == []
}

/// Dart `LintIssue.toString()` shape: `[CODE] @pc N: message`.
pub fn issue_to_string(issue: LintIssue) -> String {
  "[" <> issue.code <> "] @pc " <> int.to_string(issue.pc) <> ": " <> issue.message
}

// Issue codes (stable strings, mirroring the Dart linter's code-per-check).
const invalid_operand = "INVALID_OPERAND"

const operand_arity_mismatch = "OPERAND_ARITY_MISMATCH"

const undefined_label = "UNDEFINED_LABEL"

const undefined_import = "UNDEFINED_IMPORT"

const body_op_in_head = "BODY_OP_IN_HEAD"

const head_op_in_body = "HEAD_OP_IN_BODY"

const guard_op_in_body = "GUARD_OP_IN_BODY"

const op_outside_clause = "OP_OUTSIDE_CLAUSE"

const misplaced_control = "MISPLACED_CONTROL"

const truncated_clause = "TRUNCATED_CLAUSE"

// ============================================================================
// Entry point
// ============================================================================

/// Where the linear scan currently stands (§2/§3 clause bracketing).
type Phase {
  /// Between clauses / procedures (before a `clause_try`, after a clause end).
  PredicateLevel
  /// Inside a clause, before `commit` (HEAD matching + GUARD evaluation).
  HeadGuard
  /// Inside a clause, after `commit` (BODY).
  Body
}

/// Lint one compiled program: operand validity for every instruction, plus the
/// HEAD/GUARD/BODY phase-placement state machine over the stream.
pub fn lint(prog: BytecodeProgram) -> LintResult {
  let ops = program.to_ops(prog)
  let #(phase, rev_issues, next_pc) =
    list.index_fold(ops, #(PredicateLevel, [], 0), fn(acc, op, pc) {
      let #(phase, rev_issues, _) = acc
      let rev_issues =
        list.fold(operand_issues(op, prog, pc), rev_issues, fn(acc, issue) {
          [issue, ..acc]
        })
      let #(phase, rev_issues) = step_phase(phase, op, pc, rev_issues)
      #(phase, rev_issues, pc + 1)
    })
  // A stream may not end inside an open clause (§9.3: every body ends in
  // proceed; every pre-commit clause exit is clause_next/try_next_clause).
  let rev_issues = case phase {
    PredicateLevel -> rev_issues
    HeadGuard | Body -> [
      LintIssue(
        truncated_clause,
        "instruction stream ends inside an open clause (no proceed / clause exit)",
        next_pc,
      ),
      ..rev_issues
    ]
  }
  LintResult(list.reverse(rev_issues))
}

// ============================================================================
// Phase placement (spec §2, §3, §4, §7-exception, §9)
// ============================================================================

fn step_phase(
  phase: Phase,
  op: Op,
  pc: Int,
  rev_issues: List(LintIssue),
) -> #(Phase, List(LintIssue)) {
  case phase {
    PredicateLevel ->
      case op {
        opcodes.Label(..) | opcodes.Nop -> #(phase, rev_issues)
        opcodes.ClauseTry -> #(HeadGuard, rev_issues)
        opcodes.NoMoreClauses -> #(phase, rev_issues)
        _ -> #(phase, [
          LintIssue(
            op_outside_clause,
            opcodes.mnemonic(op) <> " at predicate level (outside any clause_try…clause-end bracket)",
            pc,
          ),
          ..rev_issues
        ])
      }
    HeadGuard ->
      case op {
        opcodes.Commit -> #(Body, rev_issues)
        // Pre-commit clause exits return to predicate level (§2.2/§2.3).
        opcodes.ClauseNext(..) | opcodes.TryNextClause -> #(
          PredicateLevel,
          rev_issues,
        )
        opcodes.ClauseTry -> #(phase, [
          LintIssue(
            misplaced_control,
            "clause_try inside an open clause (previous clause not closed)",
            pc,
          ),
          ..rev_issues
        ])
        opcodes.NoMoreClauses -> #(PredicateLevel, [
          LintIssue(
            misplaced_control,
            "no_more_clauses inside an open clause",
            pc,
          ),
          ..rev_issues
        ])
        _ ->
          case is_body_only(op) {
            True -> {
              let rev_issues = [
                LintIssue(
                  body_op_in_head,
                  "body-only op " <> opcodes.mnemonic(op) <> " before commit (spec §4.2: BODY only)",
                  pc,
                ),
                ..rev_issues
              ]
              // Resync after reporting: a misplaced body-only op still has its
              // own bracketing effect, and `proceed` CLOSES the clause (§9.3).
              // Staying in HeadGuard would blame the next, well-formed clause
              // for this one's defect (codexreview 064 cycle-2
              // lint-phase-desync-cascade).
              case op {
                opcodes.Proceed -> #(PredicateLevel, rev_issues)
                _ -> #(phase, rev_issues)
              }
            }
            // Head/get/guard families and the structure/put families (put-*
            // pre-commit is the §7 guard-argument-setup exception).
            False -> #(phase, rev_issues)
          }
      }
    Body ->
      case op {
        opcodes.Proceed -> #(PredicateLevel, rev_issues)
        opcodes.Commit -> #(phase, [
          LintIssue(misplaced_control, "redundant commit in body", pc),
          ..rev_issues
        ])
        opcodes.ClauseTry -> #(HeadGuard, [
          LintIssue(
            misplaced_control,
            "clause_try in body (previous clause missing its proceed)",
            pc,
          ),
          ..rev_issues
        ])
        opcodes.ClauseNext(..) | opcodes.TryNextClause -> #(PredicateLevel, [
          LintIssue(
            misplaced_control,
            opcodes.mnemonic(op) <> " in body (clause-selection control is pre-commit only)",
            pc,
          ),
          ..rev_issues
        ])
        opcodes.NoMoreClauses -> #(PredicateLevel, [
          LintIssue(misplaced_control, "no_more_clauses in body", pc),
          ..rev_issues
        ])
        _ ->
          case is_head_only(op), is_guard_op(op) {
            True, _ -> #(phase, [
              LintIssue(
                head_op_in_body,
                "HEAD-phase op " <> opcodes.mnemonic(op) <> " after commit",
                pc,
              ),
              ..rev_issues
            ])
            _, True -> #(phase, [
              LintIssue(
                guard_op_in_body,
                "GUARD op " <> opcodes.mnemonic(op) <> " after commit",
                pc,
              ),
              ..rev_issues
            ])
            False, False -> #(phase, rev_issues)
          }
      }
  }
}

/// Ops legal ONLY after commit (§4.2 allocate/deallocate; §9 spawn/requeue/
/// proceed; §14 halt; module RPC distribute/transmit — all goal-scheduling or
/// frame/heap mutators).
fn is_body_only(op: Op) -> Bool {
  case op {
    opcodes.Spawn(..)
    | opcodes.Requeue(..)
    | opcodes.Proceed
    | opcodes.Allocate(..)
    | opcodes.Deallocate
    | opcodes.Halt
    | opcodes.Distribute(..)
    | opcodes.Transmit(..) -> True
    _ -> False
  }
}

/// Ops legal ONLY before commit (§6 head matching, §12 mode-aware argument
/// loading — they read args and record tentative bindings in σ̂w).
fn is_head_only(op: Op) -> Bool {
  case op {
    opcodes.HeadStructure(..)
    | opcodes.HeadVariable(..)
    | opcodes.HeadConstant(..)
    | opcodes.HeadNil(..)
    | opcodes.HeadList(..)
    | opcodes.GetVariable(..)
    | opcodes.GetValue(..) -> True
    _ -> False
  }
}

/// §11 guard instructions — three-valued tests, GUARD phase only.
fn is_guard_op(op: Op) -> Bool {
  case op {
    opcodes.Guard(..)
    | opcodes.Ground(..)
    | opcodes.Known(..)
    | opcodes.Unknown(..)
    | opcodes.Otherwise
    | opcodes.NoReaders(..)
    | opcodes.GroundEqual(..) -> True
    _ -> False
  }
}

// ============================================================================
// Operand validity (per-instruction, phase-independent)
// ============================================================================

fn operand_issues(op: Op, prog: BytecodeProgram, pc: Int) -> List(LintIssue) {
  case op {
    opcodes.Label(name) -> require(name != "", "empty label name", pc)
    opcodes.ClauseNext(label) ->
      case program.label_pc(prog, label) {
        Ok(_) -> []
        Error(_) -> [
          LintIssue(
            undefined_label,
            "clause_next target \"" <> label <> "\" is not a label in this program",
            pc,
          ),
        ]
      }
    opcodes.Spawn(label, arity) | opcodes.Requeue(label, arity) ->
      list.append(
        require(arity >= 0, "negative arity", pc),
        label_arity_issues(op, label, arity, pc),
      )
    opcodes.Allocate(slots) -> require(slots >= 0, "negative slot count", pc)
    opcodes.HeadStructure(functor, arity, arg_slot) ->
      structure_operand_issues(functor, arity, pc)
      |> list.append(require(arg_slot >= 0, "negative argument register", pc))
    opcodes.HeadVariable(var_index, _) ->
      require(var_index >= 0, "negative variable register", pc)
    opcodes.HeadConstant(_, arg_slot)
    | opcodes.HeadNil(arg_slot)
    | opcodes.HeadList(arg_slot) ->
      require(arg_slot >= 0, "negative argument register", pc)
    opcodes.GetVariable(var_index, arg_slot, _)
    | opcodes.GetValue(var_index, arg_slot, _) ->
      require(var_index >= 0, "negative variable register", pc)
      |> list.append(require(arg_slot >= 0, "negative argument register", pc))
    // put_structure arg_slot -1 is the build-at-S nested position the
    // production codegen emits (codegen.gleam gen_arg_struct_elem) — legal.
    opcodes.PutStructure(functor, arity, arg_slot) ->
      structure_operand_issues(functor, arity, pc)
      |> list.append(require(
        arg_slot >= -1,
        "argument register below -1 (only -1 marks the at-S position)",
        pc,
      ))
    opcodes.PutVariable(var_index, arg_slot, _) ->
      require(var_index >= 0, "negative variable register", pc)
      |> list.append(require(arg_slot >= 0, "negative argument register", pc))
    opcodes.PutConstant(_, arg_slot)
    | opcodes.PutNil(arg_slot)
    | opcodes.PutList(arg_slot)
    | opcodes.PutBoundConst(_, arg_slot)
    | opcodes.PutBoundNil(arg_slot) ->
      require(arg_slot >= 0, "negative argument register", pc)
    opcodes.UnifyVariable(var_index, _) | opcodes.SetVariable(var_index, _) ->
      require(var_index >= 0, "negative variable register", pc)
    opcodes.UnifyVoid(count) ->
      require(count >= 1, "void count below 1 (§8.4: void n, n ≥ 1)", pc)
    opcodes.Push(reg_index) | opcodes.Pop(reg_index) ->
      require(reg_index >= 0, "negative save register", pc)
    opcodes.UnifyStructure(functor, arity) ->
      structure_operand_issues(functor, arity, pc)
    opcodes.Guard(procedure_label, arity, _) ->
      require(procedure_label != "", "empty guard predicate", pc)
      |> list.append(require(arity >= 0, "negative arity", pc))
    opcodes.Ground(var_index, _)
    | opcodes.Known(var_index, _)
    | opcodes.Unknown(var_index)
    | opcodes.NoReaders(var_index, _) ->
      require(var_index >= 0, "negative variable register", pc)
    opcodes.GroundEqual(left, right, _) ->
      require(left >= 0, "negative left variable register", pc)
      |> list.append(require(right >= 0, "negative right variable register", pc))
    opcodes.Distribute(import_index, functor, arity) ->
      structure_operand_issues(functor, arity, pc)
      |> list.append(require(import_index >= 1, "import index below 1 (1-based)", pc))
      |> list.append(case
        import_index >= 1 && program.import_name(prog, import_index) == Error(Nil)
      {
        True -> [
          LintIssue(
            undefined_import,
            "distribute import index " <> int.to_string(import_index) <> " is not in the program's import table",
            pc,
          ),
        ]
        False -> []
      })
    opcodes.Transmit(module_var_index, functor, arity) ->
      structure_operand_issues(functor, arity, pc)
      |> list.append(require(module_var_index >= 0, "negative module register", pc))
    // Operand-free control ops.
    opcodes.ClauseTry
    | opcodes.TryNextClause
    | opcodes.NoMoreClauses
    | opcodes.Commit
    | opcodes.Proceed
    | opcodes.Deallocate
    | opcodes.Nop
    | opcodes.Halt
    | opcodes.UnifyConstant(_)
    | opcodes.SetConstant(_)
    | opcodes.Otherwise -> []
  }
}

fn structure_operand_issues(
  functor: String,
  arity: Int,
  pc: Int,
) -> List(LintIssue) {
  require(functor != "", "empty functor", pc)
  |> list.append(require(arity >= 0, "negative functor arity", pc))
}

/// A spawn/requeue procedure label is "name/arity" (§9.1 spawn P/n; codegen
/// emits `functor <> "/" <> arity`); its trailing arity must agree with the
/// arity operand — the v2.16 analogue of a wrong operand count.
fn label_arity_issues(
  op: Op,
  label: String,
  arity: Int,
  pc: Int,
) -> List(LintIssue) {
  let mismatch = fn(detail) {
    [
      LintIssue(
        operand_arity_mismatch,
        opcodes.mnemonic(op) <> " label \"" <> label <> "\" " <> detail,
        pc,
      ),
    ]
  }
  case string.split(label, "/") {
    [_] | [] -> mismatch("carries no /arity suffix")
    parts ->
      case list.last(parts) {
        Ok(suffix) ->
          case int.parse(suffix) {
            Ok(label_arity) ->
              case label_arity == arity {
                True -> []
                False ->
                  mismatch(
                    "declares arity "
                    <> suffix
                    <> " but the arity operand is "
                    <> int.to_string(arity),
                  )
              }
            Error(_) -> mismatch("carries a non-numeric /arity suffix")
          }
        Error(_) -> mismatch("carries no /arity suffix")
      }
  }
}

fn require(condition: Bool, detail: String, pc: Int) -> List(LintIssue) {
  case condition {
    True -> []
    False -> [LintIssue(invalid_operand, detail, pc)]
  }
}
