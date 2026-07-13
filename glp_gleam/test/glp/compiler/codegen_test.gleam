//// Codegen smoke test (feature 050, T019) — byte-comparable output on the P5
//// merge example.
////
//// The Dart `CodeGenerator` is the parity oracle. This pins the exact v2.16
//// instruction stream produced for a canonical two-clause `merge/3`, derived
//// from the Dart codegen algorithm (glp_runtime/lib/compiler/codegen.dart):
//// register indices follow first-occurrence order across HEAD → GUARDS → BODY
//// (X=0, Xs=1, Ys=2, Zs=3 in clause 1; Ys=0 in clause 2), head list cells emit
//// head_structure './2' + unify_reader/writer, body goals emit put_* + spawn,
//// and the procedure is wrapped by its entry label, the per-clause `_c<n>`
//// labels, and `_end` + no_more_clauses.

import glp/bytecode/opcodes.{
  ClauseTry, Commit, GetValue, GetVariable, HeadNil, HeadStructure, Label,
  NoMoreClauses, Proceed, PutVariable, Spawn, UnifyVariable,
}
import glp/bytecode/program
import glp/compiler/codegen
import glp/parser/lexer
import glp/parser/parser
import gleeunit/should

const merge_source = "merge([X|Xs], Ys, [X|Zs]) :- merge(Xs?, Ys?, Zs).
merge([], Ys, Ys)."

fn compile(source: String) -> program.BytecodeProgram {
  let assert Ok(tokens) = lexer.tokenize(source)
  let assert Ok(module) = parser.parse_module(tokens)
  codegen.generate(module)
}

pub fn merge_bytecode_parity_test() {
  let ops = program.to_ops(compile(merge_source))

  ops
  |> should.equal([
    Label("merge/3"),
    // clause 1: merge([X|Xs], Ys, [X|Zs]) :- merge(Xs?, Ys?, Zs).
    ClauseTry,
    HeadStructure(".", 2, 0),
    UnifyVariable(0, False),
    // X
    UnifyVariable(1, False),
    // Xs
    GetVariable(2, 1, False),
    // Ys
    HeadStructure(".", 2, 2),
    UnifyVariable(0, False),
    // X (shared register, not a Get — inside a structure)
    UnifyVariable(3, False),
    // Zs
    Commit,
    PutVariable(1, 0, True),
    // Xs?
    PutVariable(2, 1, True),
    // Ys?
    PutVariable(3, 2, False),
    // Zs
    Spawn("merge/3", 3),
    Proceed,
    // clause 2: merge([], Ys, Ys).
    Label("merge/3_c1"),
    ClauseTry,
    HeadNil(0),
    GetVariable(0, 1, False),
    // Ys (first occurrence)
    GetValue(0, 2, False),
    // Ys (subsequent occurrence → get_value)
    Commit,
    Proceed,
    // procedure epilogue
    Label("merge/3_end"),
    NoMoreClauses,
  ])
}
