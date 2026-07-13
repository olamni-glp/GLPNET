//// Parameterized-type expansion tests (feature 050, T018) — Dart
//// param_expansion.dart is the oracle: angle-bracket expanded names,
//// wildcard-instantiated concrete decls + preserved templates, Stream(_) ≡
//// Stream collapse, nested instantiation.

import gleam/dict
import gleam/list
import gleam/set
import gleam/string
import gleeunit/should
import glp/analysis/type_ast.{type TypeDef}
import glp/analysis/type_checker/param_expansion
import glp/parser/ast
import glp/parser/lexer
import glp/parser/parser

fn parse(source: String) -> ast.SourceModule {
  let assert Ok(tokens) = lexer.tokenize(source)
  let assert Ok(module) = parser.parse_module(tokens)
  module
}

fn expand(source: String) -> ast.SourceModule {
  param_expansion.expand_parameterized_types(parse(source), set.new(), dict.new())
}

fn render_type_defs(module: ast.SourceModule) -> List(String) {
  list.map(module.type_defs, render_type_def)
}

fn render_type_def(td: TypeDef) -> String {
  td.name
  <> " ::= "
  <> {
    td.alternatives
    |> list.map(type_ast.type_expr_to_string)
    |> string.join(" ; ")
  }
  <> "."
}

fn render_decl_args(module: ast.SourceModule) -> List(List(String)) {
  list.map(module.proc_declarations, fn(decl) {
    list.map(decl.arg_types, type_ast.type_expr_to_string)
  })
}

// ── Concrete instantiation from a procedure declaration ────────────────────

pub fn concrete_instantiation_expands_test() {
  let module =
    expand(
      "Stream(X) ::= [] ; [X | Stream(X)].\n"
      <> "AgentMsg ::= a ; b.\n"
      <> "procedure merge(Stream(AgentMsg)?, Stream(AgentMsg)?, Stream(AgentMsg)).\n"
      <> "merge(_, _, _).",
    )
  render_type_defs(module)
  |> should.equal([
    "AgentMsg ::= a ; b.",
    "Stream<AgentMsg> ::= [] ; [AgentMsg | Stream<AgentMsg>].",
  ])
  render_decl_args(module)
  |> should.equal([
    ["Stream<AgentMsg>?", "Stream<AgentMsg>?", "Stream<AgentMsg>"],
  ])
  module.param_proc_decls |> should.equal([])
}

// ── Parameterized proc decl: template preserved + wildcard concrete ────────

pub fn parameterized_decl_yields_template_and_wildcard_test() {
  let module =
    expand(
      "Stream(X) ::= [] ; [X | Stream(X)].\n"
      <> "procedure gethead(Stream(X)?, X).\n"
      <> "gethead([H|_], H?).",
    )
  // The template is preserved for call-site inference, with detected params.
  let assert [template] = module.param_proc_decls
  template.type_params |> should.equal(["X"])
  list.map(template.arg_types, type_ast.type_expr_to_string)
  |> should.equal(["Stream(X)?", "X"])
  // The concrete version substitutes each param with `_`; no monomorphic
  // Stream exists, so Stream(_) expands to Stream<_>.
  render_decl_args(module) |> should.equal([["Stream<_>?", "_"]])
  render_type_defs(module)
  |> should.equal(["Stream<_> ::= [] ; [_ | Stream<_>]."])
}

pub fn wildcard_collapses_to_known_mono_name_test() {
  let module =
    param_expansion.expand_parameterized_types(
      parse(
        "Stream(X) ::= [] ; [X | Stream(X)].\n"
        <> "procedure gethead(Stream(X)?, X).\n"
        <> "gethead([H|_], H?).",
      ),
      set.from_list(["Stream"]),
      dict.new(),
    )
  // Stream(_) ≡ Stream when a monomorphic Stream is known.
  render_decl_args(module) |> should.equal([["Stream?", "_"]])
  render_type_defs(module) |> should.equal([])
}

// ── External (prelude/ancestor) templates ───────────────────────────────────

pub fn external_template_expands_test() {
  let prelude = parse("Stream(X) ::= [] ; [X | Stream(X)].")
  let external =
    list.fold(prelude.type_defs, dict.new(), fn(acc, td) {
      dict.insert(acc, td.name, td)
    })
  let module =
    param_expansion.expand_parameterized_types(
      parse("Msg ::= m(Integer).\nprocedure p(Stream(Msg)?).\np(_)."),
      set.new(),
      external,
    )
  render_type_defs(module)
  |> should.equal([
    "Msg ::= m(Integer).",
    "Stream<Msg> ::= [] ; [Msg | Stream<Msg>].",
  ])
  render_decl_args(module) |> should.equal([["Stream<Msg>?"]])
}

// ── Nested instantiation ────────────────────────────────────────────────────

pub fn nested_instantiation_expands_both_levels_test() {
  let module =
    expand(
      "Stream(X) ::= [] ; [X | Stream(X)].\n"
      <> "procedure q(Stream(Stream(Integer))?).\n"
      <> "q(_).",
    )
  render_decl_args(module) |> should.equal([["Stream<Stream<Integer>>?"]])
  // Both levels get expanded definitions (worklist order: outer registered
  // first from the declaration scan, then the nested arg).
  render_type_defs(module)
  |> should.equal([
    "Stream<Stream<Integer>> ::= [] ; [Stream<Integer> | Stream<Stream<Integer>>].",
    "Stream<Integer> ::= [] ; [Integer | Stream<Integer>].",
  ])
}

// ── Canonical names carry inner modes ───────────────────────────────────────

pub fn canonical_name_preserves_inner_input_mode_test() {
  let module =
    expand(
      "Pair(A, B) ::= pair(A, B).\n"
      <> "Msg ::= m.\n"
      <> "procedure p(Pair(Msg?, Integer)).\n"
      <> "p(_).",
    )
  render_decl_args(module) |> should.equal([["Pair<Msg?,Integer>"]])
}
