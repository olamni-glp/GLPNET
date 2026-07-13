//// Lexer conformance smoke tests (feature 050, T013) — representative branch
//// coverage against the Dart oracle's documented behaviour: clause syntax,
//// multi-char operators, readers, numbers, strings vs quoted atoms, keywords,
//// comments, and the lexer error cases.

import gleam/list
import gleam/option.{None, Some}
import gleeunit/should
import glp/parser/lexer.{
  type Token, ArithEqual, Assign, AtLessEqual, ColonColonEq, Dot, Eof,
  GroundEqual, Implies, LexError, LitInt, LitReal, LitString, LParen, Minus,
  Mod, Pipe, Procedure, Question, RParen, SlashSlash, Token, TokAtom,
  TokNumber, TokReader, TokString, TokVariable, Underscore, Univ,
  UnivDecompose,
}

fn types_of(tokens: List(Token)) -> List(lexer.TokenType) {
  list.map(tokens, fn(t) { t.token_type })
}

pub fn clause_tokens_test() {
  let assert Ok(tokens) = lexer.tokenize("p(X, Ys?) :- true | q(X?).")
  types_of(tokens)
  |> should.equal([
    TokAtom, LParen, TokVariable, lexer.Comma, TokReader, RParen, Implies,
    TokAtom, Pipe, TokAtom, LParen, TokReader, RParen, Dot, Eof,
  ])
}

pub fn reader_token_keeps_base_name_test() {
  let assert Ok([reader, ..]) = lexer.tokenize("Ys?")
  reader |> should.equal(Token(TokReader, "Ys", 1, 1, None))
}

pub fn multi_char_operators_test() {
  let assert Ok(tokens) = lexer.tokenize(":- := ::= =:= =?= =.. ..= @=< //")
  types_of(tokens)
  |> should.equal([
    Implies, Assign, ColonColonEq, ArithEqual, GroundEqual, Univ,
    UnivDecompose, AtLessEqual, SlashSlash, Eof,
  ])
}

pub fn numbers_test() {
  let assert Ok([a, b, c, _eof]) = lexer.tokenize("42 3.14 -17")
  a.literal |> should.equal(Some(LitInt(42)))
  b.literal |> should.equal(Some(LitReal(3.14)))
  c.literal |> should.equal(Some(LitInt(-17)))
}

pub fn number_dot_is_clause_terminator_test() {
  // "3." is NUMBER(3) DOT — the decimal point needs a following digit.
  let assert Ok(tokens) = lexer.tokenize("f(3).")
  types_of(tokens)
  |> should.equal([TokAtom, LParen, TokNumber, RParen, Dot, Eof])
}

pub fn minus_operator_vs_negative_literal_test() {
  let assert Ok(tokens) = lexer.tokenize("X - 1")
  types_of(tokens) |> should.equal([TokVariable, Minus, TokNumber, Eof])
}

pub fn double_quoted_is_string_single_quoted_is_atom_test() {
  let assert Ok([s, a, _eof]) = lexer.tokenize("\"hi\\n\" 'Quoted Atom'")
  s.token_type |> should.equal(TokString)
  s.literal |> should.equal(Some(LitString("hi\n")))
  a.token_type |> should.equal(TokAtom)
  a.lexeme |> should.equal("Quoted Atom")
}

pub fn mod_keyword_vs_mod_call_test() {
  let assert Ok([kw, ..]) = lexer.tokenize("5 mod 2")
  kw.token_type |> should.equal(TokNumber)
  let assert Ok(tokens) = lexer.tokenize("X mod Y")
  types_of(tokens) |> should.equal([TokVariable, Mod, TokVariable, Eof])
  // mod( is a predicate call, not the keyword.
  let assert Ok(tokens) = lexer.tokenize("mod(X)")
  types_of(tokens)
  |> should.equal([TokAtom, LParen, TokVariable, RParen, Eof])
}

pub fn procedure_keyword_test() {
  let assert Ok([kw, ..]) = lexer.tokenize("procedure p(Integer?).")
  kw.token_type |> should.equal(Procedure)
}

pub fn underscore_and_named_anonymous_test() {
  let assert Ok(tokens) = lexer.tokenize("_ _Out _abc")
  types_of(tokens)
  // _ alone → UNDERSCORE; _Out → VARIABLE (named anonymous); _abc → ATOM.
  |> should.equal([Underscore, TokVariable, TokAtom, Eof])
}

pub fn anonymous_reader_test() {
  // _? lexes as UNDERSCORE QUESTION (the parser assembles the reader).
  let assert Ok(tokens) = lexer.tokenize("_?")
  types_of(tokens) |> should.equal([Underscore, Question, Eof])
}

pub fn comments_are_skipped_test() {
  let assert Ok(tokens) =
    lexer.tokenize("p. % line comment\n/* block\ncomment */ q.")
  types_of(tokens) |> should.equal([TokAtom, Dot, TokAtom, Dot, Eof])
}

pub fn line_and_column_tracking_test() {
  let assert Ok([p, _dot, q, ..]) = lexer.tokenize("p.\n  q.")
  p.line |> should.equal(1)
  p.column |> should.equal(1)
  q.line |> should.equal(2)
  q.column |> should.equal(3)
}

pub fn unterminated_string_is_an_error_test() {
  lexer.tokenize("\"never closed")
  |> should.equal(Error(LexError("Unterminated string", 1, 1)))
}

pub fn lone_colon_is_an_error_test() {
  lexer.tokenize("p : q")
  |> should.equal(Error(LexError("Unexpected character :", 1, 3)))
}

pub fn crlf_line_endings_test() {
  // CRLF is one grapheme cluster in Gleam; the Dart code-unit oracle skips
  // '\r' as whitespace and bumps the line on '\n' — net line+1, column 1.
  let assert Ok([p, _dot, q, ..]) = lexer.tokenize("p.\r\n  q. % c\r\nr.")
  p.line |> should.equal(1)
  q.line |> should.equal(2)
  q.column |> should.equal(3)
  let assert Ok(tokens) = lexer.tokenize("p.\r\n% comment\r\nq.")
  types_of(tokens) |> should.equal([TokAtom, Dot, TokAtom, Dot, Eof])
}
