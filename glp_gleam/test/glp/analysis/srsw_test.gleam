//// SRSW checker conformance tests (feature 050, T016) — the Dart analyzer's
//// STEP-1 validation is the oracle: violation message text, relaxations
//// (ground-implying guards, constant-type head args), anonymous exemption,
//// guard-position and reserved-constant errors.

import gleeunit/should
import glp/analysis/srsw.{
  GuardPositionError, ReservedConstantError, SrswViolations,
}
import glp/analysis/type_ast.{Pos}
import glp/parser/lexer
import glp/parser/parser

fn check(source: String) -> Result(Nil, srsw.SrswError) {
  let assert Ok(tokens) = lexer.tokenize(source)
  let assert Ok(module) = parser.parse_module(tokens)
  srsw.check(module)
}

// ── Valid programs ──────────────────────────────────────────────────────────

pub fn basic_pairing_ok_test() {
  check("p(X) :- q(X?).") |> should.equal(Ok(Nil))
}

pub fn append_ok_test() {
  // Head-body flow (manual §2.3): output tail is a reader hole in the head,
  // filled by the writer in the recursive body call.
  check(
    "append([], Ys, Ys?).\nappend([X|Xs], Ys, [X?|Zs?]) :- append(Xs?, Ys?, Zs).",
  )
  |> should.equal(Ok(Nil))
}

pub fn guard_reader_satisfies_pairing_test() {
  // A reader occurring only in the guard still pairs the writer (guard
  // occurrences count toward pairing, not multiplicity).
  check("p(X) :- known(X?) | true.") |> should.equal(Ok(Nil))
}

pub fn ground_guard_allows_multiple_readers_test() {
  check("p(X) :- ground(X?) | q(X?), r(X?).") |> should.equal(Ok(Nil))
}

pub fn comparison_guard_grounds_expression_vars_test() {
  // X and Y appear inside an arithmetic comparison — both grounded, so the
  // extra body readers are allowed.
  check("p(X, Y) :- X? + 1 < Y? | q(X?, Y?), r(X?, Y?).")
  |> should.equal(Ok(Nil))
}

pub fn constant_type_relaxation_test() {
  check("procedure p(Integer?).\np(V) :- q(V?), r(V?).")
  |> should.equal(Ok(Nil))
}

pub fn anonymous_variables_exempt_test() {
  check("p(_, _X) :- q(_Y?).") |> should.equal(Ok(Nil))
}

pub fn system_mode_allows_reserved_constants_test() {
  check("-mode(system).\np('_user').") |> should.equal(Ok(Nil))
}

// ── SRSW violations (exact aggregated message) ─────────────────────────────

pub fn two_readers_without_ground_test() {
  check("p(X) :- q(X?), r(X?).")
  |> should.equal(
    Error(SrswViolations(
      "SRSW violations found:\n  • p/1: Line 1: Reader variable \"X?\" occurs 2 times without ground guard or constant type",
      Pos(0, 0),
    )),
  )
}

pub fn no_writer_and_no_reader_test() {
  // X: writer with no reader; Y: reader with no writer — both reported, in
  // first-occurrence order.
  check("p(X) :- q(Y?).")
  |> should.equal(
    Error(SrswViolations(
      "SRSW violations found:\n"
        <> "  • p/1: Line 1: Variable \"X\" has no reader (must have exactly one)\n"
        <> "  • p/1: Line 1: Variable \"Y\" has no writer (must have exactly one)",
      Pos(0, 0),
    )),
  )
}

pub fn two_writers_test() {
  check("p(X, X) :- q(X?).")
  |> should.equal(
    Error(SrswViolations(
      "SRSW violations found:\n  • p/2: Line 1: Writer variable \"X\" occurs 2 times without ground guard or constant type",
      Pos(0, 0),
    )),
  )
}

pub fn violations_aggregate_across_procedures_test() {
  let assert Error(SrswViolations(message, _)) =
    check("p(X) :- q(X?), q(X?).\nr(Y) :- s(Y?), s(Y?).")
  message
  |> should.equal(
    "SRSW violations found:\n"
    <> "  • p/1: Line 1: Reader variable \"X?\" occurs 2 times without ground guard or constant type\n"
    <> "  • r/1: Line 2: Reader variable \"Y?\" occurs 2 times without ground guard or constant type",
  )
}

pub fn guard_reader_does_not_relax_multiplicity_test() {
  // known/1 is NOT ground-implying; two body readers still violate.
  check("p(X) :- known(X?) | q(X?), r(X?).")
  |> should.equal(
    Error(SrswViolations(
      "SRSW violations found:\n  • p/1: Line 1: Reader variable \"X?\" occurs 2 times without ground guard or constant type",
      Pos(0, 0),
    )),
  )
}

// ── Guard-position and negation errors ──────────────────────────────────────

pub fn true_is_not_a_guard_test() {
  check("p(X) :- true | q(X?).")
  |> should.equal(
    Error(GuardPositionError(
      "\"true\" is not a guard - it can only appear in body position",
      Pos(1, 9),
    )),
  )
}

pub fn non_negatable_guard_test() {
  let assert Error(GuardPositionError(message, _)) =
    check("p(X, Y) :- ~(X? < Y?) | true.")
  message
  |> should.equal("Guard \"<\" cannot be negated (type-error semantics)")
}

pub fn negatable_guard_passes_test() {
  check("p(X) :- ~ground(X?) | q(X?).") |> should.equal(Ok(Nil))
}

// ── Reserved constants ──────────────────────────────────────────────────────

pub fn reserved_constant_in_user_mode_test() {
  let assert Error(ReservedConstantError(message, _)) = check("p('_user').")
  message
  |> should.equal(
    "Constants starting with '_' are reserved for system use: '_user'. Use -mode(system). directive for system code.",
  )
}
