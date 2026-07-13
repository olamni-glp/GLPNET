//// Parser conformance tests (feature 050, T015) — the Dart parser is the
//// oracle. Positive cases pin the SourceModule shapes the load pipeline
//// consumes; negative cases pin the exact Dart error messages and positions,
//// including the FR-036 decline corpus programs whose rejections were
//// cross-checked against the live Dart REPL (2026-07-11: identical text and
//// line:column for all three).

import gleam/list
import gleam/option.{None, Some}
import gleeunit/should
import glp/analysis/type_ast
import glp/parser/ast
import glp/parser/lexer
import glp/parser/parser.{type ParseError, ParseError}
import glp/runtime/terms

fn parse(source: String) -> ast.SourceModule {
  let assert Ok(tokens) = lexer.tokenize(source)
  let assert Ok(module) = parser.parse_module(tokens)
  module
}

fn parse_error(source: String) -> ParseError {
  let assert Ok(tokens) = lexer.tokenize(source)
  let assert Error(error) = parser.parse_module(tokens)
  error
}

// ── Module structure ────────────────────────────────────────────────────────

pub fn module_declaration_and_mode_test() {
  let module = parse("-module(m).\n-mode(system).\np(X) :- q(X?).\nq(_).")
  ast.module_name(module) |> should.equal(Some("m"))
  module.compile_mode |> should.equal(ast.System)
  list.map(module.procedures, ast.signature)
  |> should.equal(["p/1", "q/1"])
}

pub fn stdlib_is_system_mode_test() {
  let module = parse("-stdlib.\np(a).")
  module.compile_mode |> should.equal(ast.System)
}

pub fn hierarchical_module_name_test() {
  let module = parse("-module(utils.list).\np(a).")
  ast.module_name(module) |> should.equal(Some("utils.list"))
}

pub fn pending_clause_splits_procedures_by_arity_test() {
  // foo/2's first clause is parsed inside the foo/1 group and carried over
  // as the pending clause (Dart _pendingClause).
  let module = parse("foo(1).\nfoo(1, 2).\nfoo(3, 4).")
  list.map(module.procedures, ast.signature)
  |> should.equal(["foo/1", "foo/2"])
  let assert [_, foo2] = module.procedures
  list.length(foo2.clauses) |> should.equal(2)
}

// ── Clause shapes ───────────────────────────────────────────────────────────

pub fn unit_clause_has_no_guards_or_body_test() {
  let assert [procedure] = parse("foo(X, bar(X?)).").procedures
  let assert [clause] = procedure.clauses
  clause.guards |> should.equal(None)
  clause.body |> should.equal(None)
  let assert [ast.VarTerm("X", False, _), ast.StructTerm("bar", [ast.VarTerm("X", True, _)], _)] =
    clause.head.args
}

pub fn guards_and_body_split_at_pipe_test() {
  let assert [procedure] = parse("p(X) :- ground(X?) | q(X?).").procedures
  let assert [clause] = procedure.clauses
  let assert Some([ast.Guard("ground", [ast.VarTerm("X", True, _)], False, _)]) =
    clause.guards
  let assert Some([ast.Goal("q", [ast.VarTerm("X", True, _)], _)]) = clause.body
}

pub fn body_without_pipe_has_no_guards_test() {
  let assert [procedure] = parse("p(X) :- q(X?), r.").procedures
  let assert [clause] = procedure.clauses
  clause.guards |> should.equal(None)
  let assert Some([ast.Goal("q", _, _), ast.Goal("r", [], _)]) = clause.body
}

pub fn negated_guard_test() {
  let assert [procedure] = parse("p(X) :- ~ground(X?) | true.").procedures
  let assert [clause] = procedure.clauses
  let assert Some([ast.Guard("ground", _, True, _)]) = clause.guards
}

pub fn infix_comparison_guards_test() {
  let assert [procedure] =
    parse("p(X, Y) :- X? @< Y?, X? mod 2 =:= 0 | true.").procedures
  let assert [clause] = procedure.clauses
  let assert Some([
    ast.Guard("@<", [ast.VarTerm("X", True, _), ast.VarTerm("Y", True, _)], False, _),
    ast.Guard(
      "=:=",
      [
        ast.StructTerm("mod", [ast.VarTerm("X", True, _), ast.ConstTerm(terms.ConstInt(2), _)], _),
        ast.ConstTerm(terms.ConstInt(0), _),
      ],
      False,
      _,
    ),
  ]) = clause.guards
}

pub fn disjunction_guard_test() {
  let assert [procedure] =
    parse("p(X) :- (integer(X?) ; string(X?)) | true.").procedures
  let assert [clause] = procedure.clauses
  let assert Some([
    ast.Guard(";", [ast.StructTerm("integer", _, _), ast.StructTerm("string", _, _)], False, _),
  ]) = clause.guards
}

// ── Operator heads ──────────────────────────────────────────────────────────

pub fn assign_head_test() {
  let assert [procedure] = parse("D := E.").procedures
  ast.signature(procedure) |> should.equal(":=/2")
}

pub fn univ_heads_test() {
  let assert [p1] = parse("X? =.. Y.").procedures
  ast.signature(p1) |> should.equal("=../2")
  // Struct left side: foo(a) =.. L — the parsed atom becomes the left term.
  let assert [p2] = parse("foo(a) =.. L.").procedures
  ast.signature(p2) |> should.equal("=../2")
  let assert [clause] = p2.clauses
  let assert [ast.StructTerm("foo", _, _), ast.VarTerm("L", False, _)] =
    clause.head.args
}

// ── Terms ───────────────────────────────────────────────────────────────────

pub fn arithmetic_precedence_test() {
  let assert [procedure] = parse("p(R) :- R := 1 + 2 * 3.").procedures
  let assert [clause] = procedure.clauses
  let assert Some([
    ast.Goal(
      ":=",
      [
        ast.VarTerm("R", False, _),
        ast.StructTerm(
          "+",
          [
            ast.ConstTerm(terms.ConstInt(1), _),
            ast.StructTerm(
              "*",
              [ast.ConstTerm(terms.ConstInt(2), _), ast.ConstTerm(terms.ConstInt(3), _)],
              _,
            ),
          ],
          _,
        ),
      ],
      _,
    ),
  ]) = clause.body
}

pub fn unary_minus_becomes_neg_test() {
  let assert [procedure] = parse("p(R) :- R := -X?.").procedures
  let assert [clause] = procedure.clauses
  let assert Some([ast.Goal(":=", [_, ast.StructTerm("neg", [ast.VarTerm("X", True, _)], _)], _)]) =
    clause.body
}

pub fn operator_as_functor_test() {
  let assert [procedure] = parse("p(-(X?, Y?)).").procedures
  let assert [clause] = procedure.clauses
  let assert [ast.StructTerm("-", [ast.VarTerm("X", True, _), ast.VarTerm("Y", True, _)], _)] =
    clause.head.args
}

pub fn list_forms_test() {
  let assert [procedure] = parse("p([X, Y? | T], []).").procedures
  let assert [clause] = procedure.clauses
  let assert [
    ast.ListTerm(
      Some(ast.VarTerm("X", False, _)),
      Some(ast.ListTerm(Some(ast.VarTerm("Y", True, _)), Some(ast.VarTerm("T", False, _)), _)),
      _,
    ),
    ast.ListTerm(None, None, _),
  ] = clause.head.args
}

pub fn tuple_is_right_associative_test() {
  let assert [procedure] = parse("p((a, b, c)).").procedures
  let assert [clause] = procedure.clauses
  let assert [
    ast.StructTerm(
      ",",
      [
        ast.ConstTerm(terms.ConstAtom("a"), _),
        ast.StructTerm(",", [ast.ConstTerm(terms.ConstAtom("b"), _), ast.ConstTerm(terms.ConstAtom("c"), _)], _),
      ],
      _,
    ),
  ] = clause.head.args
}

pub fn string_vs_atom_constants_test() {
  let assert [procedure] = parse("p(\"hi\", foo, 'Quoted').").procedures
  let assert [clause] = procedure.clauses
  let assert [
    ast.ConstTerm(terms.ConstString("hi"), _),
    ast.ConstTerm(terms.ConstAtom("foo"), _),
    ast.ConstTerm(terms.ConstAtom("Quoted"), _),
  ] = clause.head.args
}

// ── Remote and spawn goals ──────────────────────────────────────────────────

pub fn nested_static_remote_goal_test() {
  // Hierarchical module call — RemoteGoal wraps a full Goal (the T014
  // ast.gleam correction to match the Dart AST).
  let assert [procedure] = parse("p :- ui # actors # render(X?).").procedures
  let assert [clause] = procedure.clauses
  let assert Some([
    ast.RemoteGoal(
      ast.ConstTerm(terms.ConstAtom("ui"), _),
      ast.RemoteGoal(
        ast.ConstTerm(terms.ConstAtom("actors"), _),
        ast.Goal("render", [ast.VarTerm("X", True, _)], _),
        _,
      ),
      _,
    ),
  ]) = clause.body
}

pub fn dynamic_remote_goal_test() {
  let assert [procedure] = parse("p(M) :- M? # work.").procedures
  let assert [clause] = procedure.clauses
  let assert Some([
    ast.RemoteGoal(ast.VarTerm("M", True, _), ast.Goal("work", [], _), _),
  ]) = clause.body
}

pub fn spawn_goal_test() {
  let assert [procedure] = parse("boot :- agent(a)@alice.").procedures
  let assert [clause] = procedure.clauses
  let assert Some([
    ast.SpawnGoal(ast.InnerGoal("agent", [ast.ConstTerm(terms.ConstAtom("a"), _)], _), "alice", _),
  ]) = clause.body
}

// ── Type definitions ────────────────────────────────────────────────────────

pub fn parameterized_typedef_test() {
  let module = parse("Stream(X) ::= [] ; [X | Stream(X)].\np(a).")
  let assert [def] = module.type_defs
  def.name |> should.equal("Stream")
  def.type_params |> should.equal(["X"])
  let assert [
    type_ast.ListNilAlt(_),
    type_ast.ListConsAlt(
      type_ast.TypeRef("X", False, [], _),
      type_ast.TypeRef("Stream", False, [type_ast.TypeRef("X", False, [], _)], _),
      _,
    ),
  ] = def.alternatives
}

pub fn explicit_dual_typedef_name_test() {
  let module = parse("Channel? ::= ch(Stream?, Stream)?.\np(a).")
  let assert [def] = module.type_defs
  def.name |> should.equal("Channel?")
}

pub fn difflist_alternative_test() {
  let module = parse("DiffList(X) ::= Stream(X) \\ Stream(X)?.\np(a).")
  let assert [def] = module.type_defs
  let assert [
    type_ast.DiffListAlt(
      type_ast.TypeRef("Stream", False, _, _),
      type_ast.TypeRef("Stream", True, _, _),
      _,
    ),
  ] = def.alternatives
}

// ── Procedure declarations ──────────────────────────────────────────────────

pub fn parameterized_proc_decl_test() {
  let module =
    parse(
      "procedure merge(Stream(X)?, Stream(X)?, Stream(X)).\nmerge(A, B, C) :- q(A?, B?, C).",
    )
  let assert [decl] = module.proc_declarations
  decl.name |> should.equal("merge")
  let assert [
    type_ast.TypeRef("Stream", True, [type_ast.TypeRef("X", False, [], _)], _),
    type_ast.TypeRef("Stream", True, _, _),
    type_ast.TypeRef("Stream", False, _, _),
  ] = decl.arg_types
}

pub fn exported_and_imported_decls_test() {
  let module =
    parse(
      "-module(client).\nimported procedure ui#actors#render(Integer?).\nexported procedure compute(Integer?, Integer).\ncompute(X, Y?) :- Y := X? + 1.",
    )
  let assert [imported, exported] = module.proc_declarations
  imported.imported |> should.equal(True)
  imported.name |> should.equal("render")
  imported.module_path |> should.equal(Some("ui#actors"))
  exported.exported |> should.equal(True)
  ast.exported_signatures(module) |> should.equal(["compute/2"])
}

pub fn builtin_declaration_only_test() {
  // ground/1 is a builtin: the declaration needs no clauses, even at EOF.
  let module = parse("procedure ground(_?).")
  let assert [decl] = module.proc_declarations
  let assert [type_ast.PrimitiveModeAlt(True, _)] = decl.arg_types
}

pub fn operator_named_proc_decl_test() {
  let module = parse("procedure =?=(_?, _?).")
  let assert [decl] = module.proc_declarations
  decl.name |> should.equal("=?=")
}

pub fn nullary_proc_decl_without_parens_test() {
  let module = parse("procedure boot.\nboot :- p.")
  let assert [decl] = module.proc_declarations
  decl.arg_types |> should.equal([])
}

// ── Negative cases (exact Dart messages) ────────────────────────────────────

pub fn non_contiguous_clauses_test() {
  parse_error("foo(1).\nbar(2).\nfoo(3).")
  |> should.equal(ParseError(
    "Non-contiguous clauses for \"foo/1\".\n  First group at line 1, second group at line 3.\n  All clauses for a predicate must be together in the source file.",
    3,
    1,
  ))
}

pub fn dangling_declaration_test() {
  parse_error("procedure lonely(Integer?).")
  |> should.equal(ParseError(
    "Procedure declaration for \"lonely\" has no clauses.\n  A procedure declaration must be immediately followed by its clauses.",
    1,
    1,
  ))
}

pub fn typedef_between_decl_and_clauses_test() {
  let error =
    parse_error("procedure p(Integer?).\nT ::= a.\np(X) :- integer(X?) | true.")
  error.message
  |> should.equal(
    "Type definition cannot appear between procedure declaration and its clauses.\n  Procedure \"p\" declared at line 1 needs clauses.",
  )
}

pub fn clause_between_decl_and_clauses_test() {
  let error = parse_error("procedure p(Integer?).\nq(a).\np(X) :- integer(X?) | true.")
  error.message
  |> should.equal(
    "Clause for \"q/1\" appears between procedure declaration and clauses for \"p/1\".\n  Procedure declaration at line 1 must be immediately followed by its clauses.",
  )
}

pub fn reader_mark_on_number_test() {
  parse_error("p(3?).").message
  |> should.equal("Reader mark \"?\" can only be applied to variables, not numbers")
}

pub fn reader_mark_on_structure_test() {
  parse_error("p(f(a)?).").message
  |> should.equal(
    "Reader mark \"?\" can only be applied to variables, not structures like f(...)",
  )
}

pub fn reader_mark_on_constant_test() {
  parse_error("p(X) :- q(abc?).").message
  |> should.equal(
    "Reader mark \"?\" can only be applied to variables, not constants like \"abc\"",
  )
}

pub fn reader_mark_on_list_test() {
  parse_error("p([]?).").message
  |> should.equal("Reader mark \"?\" can only be applied to variables, not lists")
}

pub fn double_negation_test() {
  parse_error("p(X) :- ~~ground(X?) | true.").message
  |> should.equal("Double negation ~~G is not allowed")
}

pub fn negation_on_disjunction_test() {
  parse_error("p(X) :- ~(integer(X?) ; string(X?)) | true.").message
  |> should.equal("Guard negation (~) cannot be applied to disjunction")
}

pub fn negation_on_remote_goal_test() {
  parse_error("p(X) :- ~m # q(X?) | true.").message
  |> should.equal("Guard negation (~) cannot be applied to remote goal")
}

pub fn bare_variable_in_goal_position_test() {
  parse_error("p(X) :- true | X.")
  |> should.equal(ParseError(
    "Expected predicate name or assignment, got variable \"X\"",
    1,
    16,
  ))
}

pub fn module_name_with_arguments_test() {
  parse_error("p :- foo(a) # bar.").message
  |> should.equal("Module name cannot have arguments: foo")
}

pub fn missing_clause_dot_test() {
  parse_error("p(X)").message |> should.equal("Expected \".\" at end of clause")
}

pub fn export_declaration_removed_test() {
  parse_error("-export([foo/1]).\nfoo(1).")
  |> should.equal(ParseError(
    "The -export() declaration is no longer supported. Use 'exported procedure' instead.",
    1,
    1,
  ))
}

// ── FR-036 decline corpus negatives (cross-checked vs Dart REPL) ───────────

pub fn decline_prolog_eq_eq_test() {
  // programs/tests/typed/decline_eq_bad.glp clause shape: '==' is two EQUALS
  // tokens; the Dart REPL rejects with this exact message.
  parse_error("deq(X, Y, yes) :- X? == Y? | true.")
  |> should.equal(ParseError("Expected term, got TokenType.EQUALS", 1, 23))
}

pub fn decline_prolog_neq_test() {
  // decline_neq_bad.glp: '\==' lexes BACKSLASH EQUALS EQUALS; the expression
  // fallback stops before BACKSLASH (precedence 1 < 6) and finds no
  // comparison operator.
  parse_error("dneq(X, Y, yes) :- X? \\== Y? | true.")
  |> should.equal(ParseError("Expected predicate name or comparison", 1, 23))
}

pub fn decline_struct_diseq_test() {
  // decline_struct_diseq_bad.glp uses '\=' (structural disequality, removed
  // from GLP): BACKSLASH EQUALS — the fallback stops at BACKSLASH.
  parse_error("ddiff(X, Y, yes) :- X? \\= Y? | true.")
  |> should.equal(ParseError("Expected predicate name or comparison", 1, 24))
}
