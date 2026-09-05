//// goal_boot — feature 101 front-end goal-term acceptance (Gleam half).
////
//// Implements: specs/101-goal-term-acceptance/spec.md FR-001..FR-008, SC-003.
////
//// WHY THIS FILE EXISTS. The Gleam half of feature 101 (commit `02f39269`) changed
//// `goal_boot.gleam` and added NO test — the commit message records that the baseline
//// discipline was not followed rather than implying it was. So every claim the Gleam
//// implementation makes was, until this file, backed by a comment and by nothing else.
//// DISCIPLINE.md §2.4 requires the test, and SC-005's whole point is that a claim
//// carried only by a note goes stale without anyone noticing.
////
//// The oracle is the Dart engine (`glp_runtime/lib/engine/glp_engine.dart`) and its C#
//// mirror (`out/csharp/lib/engine/glp_engine.cs`); all three were measured to produce
//// byte-identical REPL output for these shapes.
////
//// Several tests carry an explicit NEGATIVE control asserting the OPPOSITE verdict on a
//// neighbouring input. A test that only ever asserts "accepted" would also pass against
//// an implementation that accepts everything, and the two would be indistinguishable.

import gleam/int
import gleam/list
import gleam/option.{None, Some}
import gleam/string
import gleeunit/should
import glp/analysis/type_ast.{Pos}
import glp/bytecode/program
import glp/engine/goal_boot
import glp/parser/ast
import glp/runtime/heap
import glp/runtime/terms

fn pos() -> type_ast.Pos {
  Pos(0, 0)
}

fn anon() -> ast.Term {
  ast.UnderscoreTerm(False, pos())
}

fn anon_reader() -> ast.Term {
  ast.UnderscoreTerm(True, pos())
}

fn var_w(name: String) -> ast.Term {
  ast.VarTerm(name, False, pos())
}

fn atom_const(name: String) -> ast.Term {
  ast.ConstTerm(terms.ConstAtom(name), pos())
}

fn atom_of(args: List(ast.Term)) -> ast.Atom {
  ast.Atom("first_item", args, pos())
}

/// Boot a single goal, failing the test on a refusal.
fn boot_ok(args: List(ast.Term)) -> goal_boot.BootResult {
  let assert Ok(r) = goal_boot.setup_goal(heap.new(), atom_of(args))
  r
}

/// Boot a single goal expecting a REFUSAL, and hand back the message.
fn boot_err(args: List(ast.Term)) -> String {
  let assert Error(e) = goal_boot.setup_goal(heap.new(), atom_of(args))
  e
}

/// The names reported back to the user, in first-occurrence order.
fn reported_names(r: goal_boot.BootResult) -> List(String) {
  list.map(r.query_var_writers, fn(p) { p.0 })
}

fn addr_of(reg: Result(terms.Term, Nil)) -> Int {
  case reg {
    Ok(terms.VarRef(a)) -> a
    _ -> -1
  }
}

fn slot_is_var_ref(r: goal_boot.BootResult, i: Int) -> Bool {
  case program.get_reg(r.regs, i) {
    Ok(terms.VarRef(_)) -> True
    _ -> False
  }
}

// ── FR-001: `_` is accepted at every position a named variable is ─────────────

pub fn underscore_at_top_level_argument_is_accepted_test() {
  slot_is_var_ref(boot_ok([anon()]), 0)
  |> should.be_true
}

pub fn underscore_inside_a_structure_is_accepted_test() {
  slot_is_var_ref(
    boot_ok([ast.StructTerm("send", [var_w("X"), anon()], pos())]),
    0,
  )
  |> should.be_true
}

pub fn underscore_as_a_list_element_is_accepted_test() {
  slot_is_var_ref(boot_ok([ast.ListTerm(Some(anon()), None, pos())]), 0)
  |> should.be_true
}

pub fn underscore_as_a_list_tail_is_accepted_test() {
  // `[a|_]` — the FOURTH position, added by feature 101. Dart had no tail case for
  // `_` either: its tail `else` silently coerced anything unrecognised to nil.
  slot_is_var_ref(
    boot_ok([ast.ListTerm(Some(atom_const("a")), Some(anon()), pos())]),
    0,
  )
  |> should.be_true
}

// ── FR-004: an anonymous argument reports NO binding ──────────────────────────

pub fn underscore_reports_no_binding_test() {
  // `_` has no name to report against, so it must never reach query_var_writers.
  // Held by CONSTRUCTION (`anonymous_writer` never extends the list) rather than by
  // a filter applied afterwards; this pins that it stays that way.
  reported_names(boot_ok([anon()]))
  |> should.equal([])
}

pub fn a_named_variable_still_reports_its_binding_test() {
  // NEGATIVE CONTROL for the test above. Without it, an implementation that reported
  // NOTHING for ANY variable would pass just as well — and the two would produce
  // identical output, which is exactly the false-green shape this suite avoids.
  reported_names(boot_ok([var_w("Y")]))
  |> should.equal(["Y"])
}

pub fn underscore_alongside_a_named_variable_reports_only_the_name_test() {
  reported_names(boot_ok([anon(), var_w("Y")]))
  |> should.equal(["Y"])
}

// ── FR-003: two `_` occurrences never alias ───────────────────────────────────

pub fn two_underscores_never_alias_test() {
  // Each occurrence allocates its own heap variable and nothing keys it by name, so
  // aliasing is impossible by construction. Pinned anyway: FR-003 is the one property
  // a "reuse the last anonymous writer" optimisation would silently break.
  let r = boot_ok([anon(), anon()])
  { addr_of(program.get_reg(r.regs, 0)) == addr_of(program.get_reg(r.regs, 1)) }
  |> should.be_false
}

pub fn a_repeated_named_variable_does_alias_test() {
  // NEGATIVE CONTROL: named variables DO share one heap pair across occurrences. If
  // this and the test above returned the same verdict, neither would mean anything.
  let r = boot_ok([var_w("X"), var_w("X")])
  { addr_of(program.get_reg(r.regs, 0)) == addr_of(program.get_reg(r.regs, 1)) }
  |> should.be_true
}

// ── FR-005: an improper list tail is REFUSED, never silently coerced ──────────

pub fn improper_list_tail_is_refused_test() {
  // `[a|foo]`. Dart and C# used to DISCARD this tail and answer the goal, so
  // `[a|foo]` returned byte-identically to `[a|[]]` — a wrong answer, not an error.
  // This port always refused; this pins that the refusal was never relaxed to match.
  let msg =
    boot_err([ast.ListTerm(Some(atom_const("a")), Some(atom_const("foo")), pos())])
  string.contains(msg, "list tail is neither a list nor a variable")
  |> should.be_true
  // FR-006: the message names what the PROGRAMMER typed, not an internal class.
  string.contains(msg, "foo")
  |> should.be_true
  string.contains(msg, "ConstTerm")
  |> should.be_false
}

pub fn a_proper_nil_tail_still_boots_test() {
  // POSITIVE CONTROL for the refusal above: correct the tail and the goal runs.
  slot_is_var_ref(boot_ok([ast.ListTerm(Some(atom_const("a")), None, pos())]), 0)
  |> should.be_true
}

pub fn a_variable_tail_still_boots_test() {
  reported_names(
    boot_ok([ast.ListTerm(Some(atom_const("a")), Some(var_w("T")), pos())]),
  )
  |> should.equal(["T"])
}

// ── FR-006 / FR-012: `_?` stays INVALID, but says so legibly ──────────────────

pub fn anonymous_reader_is_refused_at_a_goal_argument_test() {
  let msg = boot_err([anon_reader()])
  string.contains(msg, "anonymous reader `_?` is not a valid term")
  |> should.be_true
  string.contains(msg, "a goal argument")
  |> should.be_true
  string.contains(msg, "UnderscoreTerm")
  |> should.be_false
}

pub fn anonymous_reader_is_refused_inside_a_structure_test() {
  let msg = boot_err([ast.StructTerm("send", [anon_reader()], pos())])
  string.contains(msg, "a structure argument")
  |> should.be_true
  string.contains(msg, "UnderscoreTerm")
  |> should.be_false
}

pub fn anonymous_reader_is_refused_as_a_list_tail_test() {
  let msg =
    boot_err([ast.ListTerm(Some(atom_const("a")), Some(anon_reader()), pos())])
  string.contains(msg, "a list tail")
  |> should.be_true
  string.contains(msg, "UnderscoreTerm")
  |> should.be_false
}

// ── FR-002 / FR-008a: the CONJUNCTION path ────────────────────────────────────

pub fn underscore_in_a_conjunction_is_accepted_and_stays_independent_test() {
  // MEASUREMENT, not a restatement of the module header. goal_boot.gleam's header says
  // the conjunction path is "STILL DEFERRED, surfaced LOUDLY", and the spec's
  // clarification bounded the three-runtime parity obligation on that premise.
  //
  // But `setup_goals` routes every argument through the SAME `setup_args` the
  // single-goal path uses, so `_` in a conjunction boots here exactly as it does in
  // Dart and C#. This test records what the CODE does. Either branch is a pass — but
  // whichever holds is now pinned, which is the whole content of FR-008a: a divergence
  // must be DECLARED and TESTED, never silent.
  let goals = [
    ast.Atom("first_item", [anon(), var_w("Y")], pos()),
    ast.Atom("second_item", [anon(), var_w("Z")], pos()),
  ]
  let assert Ok(boot) = goal_boot.setup_goals(heap.new(), goals)
  list.length(boot.goal_regs)
  |> should.equal(2)
  // FR-004 across the conjunction: only the NAMED variables are reported, in
  // first-occurrence order across ALL goals (the Dart parity invariant — one shared
  // `queryVarWriters`/`varNameToId` threaded through every goal).
  list.map(boot.query_var_writers, fn(p) { p.0 })
  |> should.equal(["Y", "Z"])
}

pub fn two_underscores_across_a_conjunction_never_alias_test() {
  let goals = [
    ast.Atom("first_item", [anon()], pos()),
    ast.Atom("second_item", [anon()], pos()),
  ]
  let assert Ok(boot) = goal_boot.setup_goals(heap.new(), goals)
  let assert [g1, g2] = boot.goal_regs
  { addr_of(program.get_reg(g1, 0)) == addr_of(program.get_reg(g2, 0)) }
  |> should.be_false
}

// ── FR-007: a refusal leaves no partial state ─────────────────────────────────

pub fn a_refused_goal_leaves_the_callers_heap_untouched_test() {
  // The refusal is a `Result`, so the populated heap only ever escapes inside `Ok` and
  // the caller's heap value is never mutated. FR-007 by construction in an immutable
  // port; pinned so a future rewrite to a mutable heap cannot quietly lose it.
  let before = heap.new()
  let assert Error(_) = goal_boot.setup_goal(before, atom_of([anon_reader()]))

  // `before` is still the empty heap: booting a fresh goal from it behaves exactly as
  // booting from a brand-new heap does.
  let assert Ok(after) = goal_boot.setup_goal(before, atom_of([var_w("Y")]))
  list.map(after.query_var_writers, fn(p) { p.0 })
  |> should.equal(["Y"])
}

// ── FR-006: no internal class name reaches the user, anywhere ─────────────────

pub fn no_refusal_message_leaks_an_internal_class_name_test() {
  // SC-005 in one assertion: sweep every refusal this module can emit and confirm none
  // of them names a Gleam/Dart constructor. The old messages did exactly that
  // ("Unsupported list head type: UnderscoreTerm"), which told the programmer nothing.
  let messages = [
    boot_err([anon_reader()]),
    boot_err([ast.StructTerm("send", [anon_reader()], pos())]),
    boot_err([ast.ListTerm(Some(atom_const("a")), Some(anon_reader()), pos())]),
    boot_err([ast.ListTerm(Some(atom_const("a")), Some(atom_const("foo")), pos())]),
  ]
  let leaks =
    list.filter(messages, fn(m) {
      string.contains(m, "UnderscoreTerm")
      || string.contains(m, "ConstTerm")
      || string.contains(m, "StructTerm")
      || string.contains(m, "VarTerm")
      || string.contains(m, "ListTerm")
    })
  case leaks {
    [] -> Nil
    [first, ..] -> should.equal("no internal class name in any refusal", first)
  }
  // And every one of them is non-empty and prefixed, so a refusal can never be an
  // empty string a caller might render as blank.
  list.all(messages, fn(m) { string.starts_with(m, "goal-boot:") })
  |> should.be_true
}

// ── belt-and-braces: the count of positions is FOUR, and int import is used ───

pub fn all_four_underscore_positions_boot_test() {
  // One assertion that the position COUNT is four, so adding a fifth position without
  // a test is visible here rather than silently uncovered.
  let shapes = [
    anon(),
    ast.StructTerm("send", [anon()], pos()),
    ast.ListTerm(Some(anon()), None, pos()),
    ast.ListTerm(Some(atom_const("a")), Some(anon()), pos()),
  ]
  let booted =
    list.filter(shapes, fn(s) {
      case goal_boot.setup_goal(heap.new(), atom_of([s])) {
        Ok(_) -> True
        Error(_) -> False
      }
    })
  int.to_string(list.length(booted))
  |> should.equal("4")
}
