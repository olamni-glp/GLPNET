//// Bytecode-lint tests (feature 064, T034).
////
//// Positive: real codegen output (via the loader) lints clean, including the
//// §7-exception shape (put-family guard-argument setup before commit) and body
//// spawns. Negative: malformed BytecodeProgram values constructed DIRECTLY
//// (never producible by codegen) — the closed `Op` union cannot carry an
//// invalid opcode id or a wrong operand COUNT, so their lintable analogues are
//// operand values no instruction is defined for (a negative register index), a
//// spawn whose "name/arity" label disagrees with its arity operand, and a
//// body-only op placed in the HEAD phase.

import gleam/dict
import gleam/list
import gleeunit/should
import glp/bytecode/lint
import glp/bytecode/opcodes
import glp/bytecode/program
import glp/compiler/loader
import glp/runtime/terms

fn codes(result: lint.LintResult) -> List(String) {
  list.map(result.issues, fn(issue) { issue.code })
}

fn lint_ops(ops: List(opcodes.Op)) -> lint.LintResult {
  lint.lint(program.from_ops(ops, dict.new()))
}

// ── Positive: production codegen shapes lint clean ──────────────────────────

// Facts only (flip/2 from the loader suite).
pub fn codegen_facts_lint_clean_test() {
  let source =
    "Bit ::= zero ; one.
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero)."
  let assert Ok(outcome) = loader.load(source, "")
  lint.lint(outcome.program)
  |> lint.is_ok
  |> should.be_true
}

// A clause body that spawns (relay shape from the scheduler suite): Spawn +
// Proceed in BODY, GetVariable/GetValue in HEAD.
pub fn codegen_body_spawn_lints_clean_test() {
  let source =
    "Bit ::= zero ; one.
procedure run_flip(Bit).
run_flip(R?) :- flip(zero, R).
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero)."
  let assert Ok(outcome) = loader.load(source, "")
  lint.lint(outcome.program)
  |> lint.is_ok
  |> should.be_true
}

// The §7 exception: put-family register loads BEFORE commit for guard argument
// setup, exactly as codegen's generic_guard emits them — must NOT be flagged.
pub fn guard_argument_setup_before_commit_is_legal_test() {
  let result =
    lint_ops([
      opcodes.Label("relay/2"),
      opcodes.ClauseTry,
      opcodes.GetVariable(0, 0, True),
      opcodes.GetVariable(1, 1, False),
      // Guard argument setup (pure register load pre-commit — §7 exception).
      opcodes.PutVariable(0, 0, True),
      opcodes.Guard("integer", 1, False),
      opcodes.Commit,
      opcodes.PutVariable(0, 0, True),
      opcodes.PutVariable(1, 1, False),
      opcodes.Spawn("out/2", 2),
      opcodes.Proceed,
      opcodes.Label("relay/2_end"),
      opcodes.NoMoreClauses,
    ])
  result
  |> lint.is_ok
  |> should.be_true
}

// ── Negative: known-bad programs, each malformation reported ────────────────

// Invalid-opcode-id analogue: HeadVariable(-1) names a register no instruction
// is defined for.
pub fn negative_register_index_reported_test() {
  let result =
    lint_ops([
      opcodes.Label("f/1"),
      opcodes.ClauseTry,
      opcodes.HeadVariable(-1, False),
      opcodes.Commit,
      opcodes.Proceed,
      opcodes.Label("f/1_end"),
      opcodes.NoMoreClauses,
    ])
  codes(result)
  |> should.equal(["INVALID_OPERAND"])
}

// Wrong-operand-count analogue: spawn label "g/2" with arity operand 3.
pub fn spawn_label_arity_mismatch_reported_test() {
  let result =
    lint_ops([
      opcodes.Label("f/0"),
      opcodes.ClauseTry,
      opcodes.Commit,
      opcodes.Spawn("g/2", 3),
      opcodes.Proceed,
      opcodes.Label("f/0_end"),
      opcodes.NoMoreClauses,
    ])
  codes(result)
  |> should.equal(["OPERAND_ARITY_MISMATCH"])
}

// Body opcode in HEAD phase: spawn before commit (§4.2).
pub fn body_op_before_commit_reported_test() {
  let result =
    lint_ops([
      opcodes.Label("f/0"),
      opcodes.ClauseTry,
      opcodes.Spawn("g/0", 0),
      opcodes.Commit,
      opcodes.Proceed,
      opcodes.Label("f/0_end"),
      opcodes.NoMoreClauses,
    ])
  codes(result)
  |> should.equal(["BODY_OP_IN_HEAD"])
}

// HEAD op after commit.
pub fn head_op_in_body_reported_test() {
  let result =
    lint_ops([
      opcodes.Label("f/1"),
      opcodes.ClauseTry,
      opcodes.Commit,
      opcodes.HeadConstant(terms.ConstAtom("zero"), 0),
      opcodes.Proceed,
      opcodes.Label("f/1_end"),
      opcodes.NoMoreClauses,
    ])
  codes(result)
  |> should.equal(["HEAD_OP_IN_BODY"])
}

// Guard op after commit.
pub fn guard_op_in_body_reported_test() {
  let result =
    lint_ops([
      opcodes.Label("f/1"),
      opcodes.ClauseTry,
      opcodes.Commit,
      opcodes.Ground(0, False),
      opcodes.Proceed,
      opcodes.Label("f/1_end"),
      opcodes.NoMoreClauses,
    ])
  codes(result)
  |> should.equal(["GUARD_OP_IN_BODY"])
}

// clause_next to a label that does not exist in the program.
pub fn undefined_clause_next_target_reported_test() {
  let result =
    lint_ops([
      opcodes.Label("f/0"),
      opcodes.ClauseTry,
      opcodes.ClauseNext("f/0_c1"),
      opcodes.Label("f/0_end"),
      opcodes.NoMoreClauses,
    ])
  codes(result)
  |> should.equal(["UNDEFINED_LABEL"])
}

// A clause whose body never closes: stream ends inside the open clause.
pub fn truncated_clause_reported_test() {
  let result =
    lint_ops([
      opcodes.Label("f/0"),
      opcodes.ClauseTry,
      opcodes.Commit,
      opcodes.Spawn("g/0", 0),
    ])
  codes(result)
  |> should.equal(["TRUNCATED_CLAUSE"])
}

// An op stranded at predicate level, outside any clause bracket.
pub fn op_outside_clause_reported_test() {
  let result =
    lint_ops([
      opcodes.Label("f/0"),
      opcodes.Proceed,
      opcodes.NoMoreClauses,
    ])
  codes(result)
  |> should.equal(["OP_OUTSIDE_CLAUSE"])
}

// Multiple malformations are all reported, in PC order.
pub fn multiple_issues_all_reported_test() {
  let result =
    lint_ops([
      opcodes.Label("f/1"),
      opcodes.ClauseTry,
      opcodes.HeadVariable(-1, False),
      opcodes.Spawn("g/1", 2),
      opcodes.Commit,
      opcodes.Ground(0, False),
      opcodes.Proceed,
      opcodes.Label("f/1_end"),
      opcodes.NoMoreClauses,
    ])
  codes(result)
  |> should.equal([
    "INVALID_OPERAND",
    "OPERAND_ARITY_MISMATCH",
    "BODY_OP_IN_HEAD",
    "GUARD_OP_IN_BODY",
  ])
}
