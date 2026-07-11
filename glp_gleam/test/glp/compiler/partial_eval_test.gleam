//// Partial-evaluator conformance tests (feature 050, T017) — the two live
//// Dart copies are the oracle: partial_evaluator.dart transformDefinedGuards
//// (engine copy: guard admission, `PE<n>` names, Remote/Spawn preserved) and
//// analyzer.dart's embedded copy (`PE_<n>` names, no admission checks,
//// Remote/Spawn flattened). Error message text is asserted byte-identical to
//// the Dart CompileError messages (REPL-verified).

import gleam/dict.{type Dict}
import gleam/list
import gleam/option.{None, Some}
import gleam/set
import gleam/string
import gleeunit/should
import glp/compiler/partial_eval.{DefinedGuardError, GuardAdmissionError}
import glp/parser/ast
import glp/parser/lexer
import glp/parser/parser
import glp/runtime/terms

fn procedures(source: String) -> List(ast.Procedure) {
  let assert Ok(tokens) = lexer.tokenize(source)
  let assert Ok(module) = parser.parse_module(tokens)
  module.procedures
}

fn units(source: String) -> Dict(String, List(ast.Term)) {
  partial_eval.collect_unit_clauses(procedures(source))
}

fn no_units() -> Dict(String, List(ast.Term)) {
  dict.new()
}

/// Render a transformed program's clauses (positions ignored) for compact
/// shape assertions. Constants render Dart-style (atoms double-quoted).
fn render(procs: List(ast.Procedure)) -> List(String) {
  list.flat_map(procs, fn(procedure) {
    list.map(procedure.clauses, render_clause)
  })
}

fn render_clause(clause: ast.Clause) -> String {
  let head = render_call(clause.head.functor, clause.head.args)
  let guards = case clause.guards {
    None -> ""
    Some(gs) -> " :- " <> string.join(list.map(gs, render_guard), ", ")
  }
  let body = case clause.body {
    None -> ""
    Some(goals) -> " | " <> string.join(list.map(goals, render_goal), ", ")
  }
  head <> guards <> body
}

fn render_call(functor: String, args: List(ast.Term)) -> String {
  case args {
    [] -> functor
    _ -> functor <> "(" <> ast.terms_to_string(args) <> ")"
  }
}

fn render_guard(guard: ast.Guard) -> String {
  let prefix = case guard.negated {
    True -> "~"
    False -> ""
  }
  prefix <> render_call(guard.predicate, guard.args)
}

fn render_goal(goal: ast.Goal) -> String {
  case goal {
    ast.Goal(functor, args, _) -> render_call(functor, args)
    ast.RemoteGoal(module, inner, _) ->
      ast.term_to_string(module) <> " # " <> render_goal(inner)
    ast.SpawnGoal(inner, agent_id, _) ->
      render_call(inner.functor, inner.args) <> "@" <> agent_id
  }
}

// ── Unit-clause collection ──────────────────────────────────────────────────

pub fn collect_unit_clauses_shapes_test() {
  let collected =
    units(
      "myg(a).\n"
      <> "with_true(b) :- true.\n"
      <> "guarded(c) :- ground(c) | true.\n"
      <> "bodied(d) :- q(d).\n"
      <> "multi(e).\nmulti(f).",
    )
  // Bare unit clause and body-`true` clause qualify; guarded, real-body, and
  // multi-clause procedures do not. (multi/1 parses as one procedure with two
  // clauses.)
  dict.keys(collected)
  |> list.sort(string.compare)
  |> should.equal(["myg/1", "with_true/1"])
}

// ── Basic unfolding ─────────────────────────────────────────────────────────

pub fn constant_unit_clause_binds_head_and_body_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("p(X) :- myg(X) | q(X?)."),
      units("myg(a)."),
    )
  // X bound to atom a; guard removed entirely (guards -> None).
  render(transformed) |> should.equal(["p(\"a\") | q(\"a\")"])
}

pub fn new_channel_unfolds_with_pe_fresh_names_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures(
        "play :- new_channel(AliceCh, BobCh) | alice(AliceCh?), bob(BobCh?).",
      ),
      units("new_channel(ch(Xs?, Ys), ch(Ys?, Xs))."),
    )
  // Manual §8.2: the channel structures flow into the body; fresh unit-clause
  // variables are PE-numbered in first-seen order (engine copy: `PE<n>`).
  render(transformed)
  |> should.equal(["play | alice(ch(PE0?, PE1)), bob(ch(PE1?, PE0))"])
}

pub fn analyzer_variant_uses_underscored_prefix_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards_analyzer(
      procedures(
        "play :- new_channel(AliceCh, BobCh) | alice(AliceCh?), bob(BobCh?).",
      ),
      units("new_channel(ch(Xs?, Ys), ch(Ys?, Xs))."),
    )
  // The analyzer copy names fresh variables `PE_<n>`.
  render(transformed)
  |> should.equal(["play | alice(ch(PE_0?, PE_1)), bob(ch(PE_1?, PE_0))"])
}

pub fn counter_is_shared_across_clauses_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("p(X) :- myg(X) | true.\nq(Y) :- myg(Y) | true."),
      units("myg(f(A?, A))."),
    )
  // One evaluator instance per run: the second clause's renaming continues
  // the counter (Dart _varCounter).
  render(transformed)
  |> should.equal(["p(f(PE0?, PE0)) | true", "q(f(PE1?, PE1)) | true"])
}

pub fn multiple_defined_guards_unfold_in_one_clause_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("p(X, Y) :- myg(X), myg2(Y) | true."),
      units("myg(a).\nmyg2(b)."),
    )
  render(transformed) |> should.equal(["p(\"a\", \"b\") | true"])
}

pub fn builtin_guard_kept_alongside_unfold_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("p(X, Y) :- ground(X?), myg(Y) | q(X?)."),
      units("myg(a)."),
    )
  render(transformed)
  |> should.equal(["p(X, \"a\") :- ground(X?) | q(X?)"])
}

pub fn user_unit_clause_overrides_prelude_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("myg(b).\np(X) :- myg(X) | true."),
      units("myg(a)."),
    )
  // Dart spread order: prelude first, user second — user definition wins.
  render(transformed) |> should.equal(["myg(\"b\")", "p(\"b\") | true"])
}

pub fn variable_to_variable_equality_guard_vanishes_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("p(X, Y) :- Y = X? | q(Y?)."),
      units("X? = X."),
    )
  // The prelude `=` unit clause: both sides are variables, so the
  // substitution only binds the renamed unit variables — the guard is
  // consumed and head/body are untouched (Dart-conformant).
  render(transformed) |> should.equal(["p(X, Y) | q(Y?)"])
}

// ── Defined-guard reduction errors (Dart phase 'analyzer') ─────────────────

pub fn negated_defined_guard_is_error_test() {
  let assert Error(DefinedGuardError(message, _)) =
    partial_eval.transform_defined_guards(
      procedures("p(X) :- ~myg(X) | true."),
      units("myg(a)."),
    )
  message |> should.equal("Defined guard \"myg\" cannot be negated")
}

pub fn failing_unfold_is_never_succeed_error_test() {
  let assert Error(DefinedGuardError(message, _)) =
    partial_eval.transform_defined_guards(
      procedures("p(_) :- myg(b) | true."),
      units("myg(a)."),
    )
  message
  |> should.equal(
    "Defined guard \"myg(\"b\")\" can never succeed.\n"
    <> "  Unit clause: myg(\"a\")\n"
    <> "  Reason: Constant mismatch: b vs a\n"
    <> "  This clause is unreachable.",
  )
}

pub fn suspending_unfold_is_not_reducible_error_test() {
  let assert Error(DefinedGuardError(message, _)) =
    partial_eval.transform_defined_guards(
      procedures("p(Y) :- myg(Y?) | true."),
      units("myg(X?)."),
    )
  message
  |> should.equal(
    "Cannot reduce defined guard \"myg(Y?)\" at compile time.\n"
    <> "  Unit clause: myg(X?)\n"
    <> "  Unbound readers: Y?\n"
    <> "  Defined guards must be fully reducible at compile time.",
  )
}

pub fn unbound_readers_list_in_insertion_order_test() {
  let assert Error(DefinedGuardError(message, _)) =
    partial_eval.transform_defined_guards(
      procedures("p(A, B) :- myg(A?, B?) | true."),
      units("myg(X?, Y?)."),
    )
  // The suspension set is insertion-ordered (Dart LinkedHashSet).
  message
  |> should.equal(
    "Cannot reduce defined guard \"myg(A?, B?)\" at compile time.\n"
    <> "  Unit clause: myg(X?, Y?)\n"
    <> "  Unbound readers: A?, B?\n"
    <> "  Defined guards must be fully reducible at compile time.",
  )
}

pub fn functor_mismatch_reason_test() {
  let assert Error(DefinedGuardError(message, _)) =
    partial_eval.transform_defined_guards(
      procedures("p(_) :- myg(f(a)) | true."),
      units("myg(g(a, b))."),
    )
  message
  |> should.equal(
    "Defined guard \"myg(f(\"a\"))\" can never succeed.\n"
    <> "  Unit clause: myg(g(\"a\", \"b\"))\n"
    <> "  Reason: Functor mismatch: f/1 vs g/2\n"
    <> "  This clause is unreachable.",
  )
}

// ── Guard admission (engine copy only; Dart phase 'partial_evaluator') ─────

pub fn non_unit_procedure_in_guard_is_admission_error_test() {
  let assert Error(GuardAdmissionError(message, _)) =
    partial_eval.transform_defined_guards(
      procedures("helper(X) :- q(X?).\np(_) :- helper(a) | true."),
      no_units(),
    )
  message
  |> should.equal(
    "Cannot call \"helper/1\" in guard position.\n"
    <> "  Only builtin guards, single-unit-clause procedures, and test-only\n"
    <> "  (runtime-defined guard) procedures can appear in guards.\n"
    <> "  The procedure \"helper\" has clauses with real bodies or\n"
    <> "  guards outside the admitted subset.",
  )
}

pub fn analyzer_variant_keeps_non_unit_guard_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards_analyzer(
      procedures("helper(X) :- q(X?).\np(Z) :- helper(Z?) | true."),
      units("unused(a)."),
    )
  // No admission checks in the analyzer copy: the guard passes through.
  render(transformed)
  |> should.equal(["helper(X) | q(X?)", "p(Z) :- helper(Z?) | true"])
}

pub fn test_only_procedure_passes_through_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("even(0).\neven(2).\np(X) :- even(X?) | true."),
      no_units(),
    )
  // 049 form (a1): a test-only procedure is a runtime-defined guard — the
  // call is kept untouched for codegen's definedGuards side-table.
  render(transformed)
  |> should.equal(["even(0)", "even(2)", "p(X) :- even(X?) | true"])
}

pub fn negated_runtime_defined_guard_is_error_test() {
  let assert Error(GuardAdmissionError(message, _)) =
    partial_eval.transform_defined_guards(
      procedures("even(0).\neven(2).\np(X) :- ~even(X?) | true."),
      no_units(),
    )
  message |> should.equal("Runtime-defined guard \"even\" cannot be negated")
}

pub fn unknown_guard_kept_for_later_phases_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("p(X) :- mystery(X?) | true."),
      no_units(),
    )
  render(transformed) |> should.equal(["p(X) :- mystery(X?) | true"])
}

// ── collect_test_only_procedures (049 greatest fixpoint) ────────────────────

pub fn test_only_fixpoint_evicts_inadmissible_guards_test() {
  let procs =
    procedures(
      "good(0).\ngood(1).\n"
      <> "composite(X) :- good(X?), ground(X?) | true.\n"
      <> "bad(X) :- helper2(X?) | true.\n"
      <> "helper2(X) :- r(X?).",
    )
  partial_eval.collect_test_only_procedures(procs)
  |> set.to_list
  |> list.sort(string.compare)
  // helper2 has a real body (never a candidate); bad's guard references it
  // and is evicted by the fixpoint. Single-clause no-guard procedures are
  // excluded by construction.
  |> should.equal(["composite/1", "good/1"])
}

// ── Goal-kind handling under substitution ──────────────────────────────────

pub fn engine_variant_preserves_remote_goal_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("p(X) :- myg(X) | m # q(X?)."),
      units("myg(a)."),
    )
  let assert [ast.Procedure(_, _, [clause], _)] = transformed
  let assert Some([
    ast.RemoteGoal(
      ast.ConstTerm(terms.ConstAtom("m"), _),
      ast.Goal("q", [ast.ConstTerm(terms.ConstAtom("a"), _)], _),
      _,
    ),
  ]) = clause.body
}

pub fn analyzer_variant_flattens_remote_goal_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards_analyzer(
      procedures("p(X) :- myg(X) | m # q(X?)."),
      units("myg(a)."),
    )
  let assert [ast.Procedure(_, _, [clause], _)] = transformed
  // The analyzer copy rebuilds a plain Goal over the uniform '#'/args view.
  let assert Some([
    ast.Goal(
      "#",
      [
        ast.ConstTerm(terms.ConstAtom("m"), _),
        ast.StructTerm("q", [ast.ConstTerm(terms.ConstAtom("a"), _)], _),
      ],
      _,
    ),
  ]) = clause.body
}

pub fn engine_variant_preserves_spawn_goal_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("p(X) :- myg(X) | q(X?)@agent1."),
      units("myg(a)."),
    )
  let assert [ast.Procedure(_, _, [clause], _)] = transformed
  let assert Some([
    ast.SpawnGoal(
      ast.InnerGoal("q", [ast.ConstTerm(terms.ConstAtom("a"), _)], _),
      "agent1",
      _,
    ),
  ]) = clause.body
}

// ── Anonymous handling divergence ───────────────────────────────────────────

pub fn engine_variant_renames_named_anonymous_unit_vars_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("p(X) :- myg(X) | true."),
      units("myg(f(_A))."),
    )
  // Engine copy: only the bare `_` is exempt from renaming — `_A` becomes a
  // PE-numbered variable inside the substituted structure.
  render(transformed) |> should.equal(["p(f(PE0)) | true"])
}

pub fn analyzer_variant_keeps_named_anonymous_unit_vars_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards_analyzer(
      procedures("p(X) :- myg(X) | true."),
      units("myg(f(_A))."),
    )
  // Analyzer copy: every '_'-prefixed name is anonymous — kept as-is.
  render(transformed) |> should.equal(["p(f(_A)) | true"])
}

pub fn bare_underscore_matches_without_binding_test() {
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(
      procedures("p(X) :- myg(X, b) | true."),
      units("myg(a, _)."),
    )
  // `_` on the unit side matches anything without binding; X still binds a.
  render(transformed) |> should.equal(["p(\"a\") | true"])
}

// ── No-op paths ─────────────────────────────────────────────────────────────

pub fn clause_without_guards_unchanged_test() {
  let source = "p(X) :- q(X?)."
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards(procedures(source), units("myg(a)."))
  transformed |> should.equal(procedures(source))
}

pub fn analyzer_variant_short_circuits_without_unit_clauses_test() {
  // With no unit clauses anywhere the analyzer copy returns the program
  // unchanged (Dart early return) — even a bodied-procedure guard survives.
  let source = "helper(X) :- q(X?).\np(Z) :- helper(Z?) | true."
  let assert Ok(transformed) =
    partial_eval.transform_defined_guards_analyzer(
      procedures(source),
      no_units(),
    )
  transformed |> should.equal(procedures(source))
}
