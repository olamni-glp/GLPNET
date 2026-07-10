//// glp/parser/lexer — lexical analyzer for GLP source (feature 050, T013).
////
//// Hand-port (R1 — no parser generator) of the Dart lexer; the Dart behaviour
//// is the conformance oracle. Token inventory 1:1 with token.dart; scan logic
//// 1:1 with lexer.dart including error messages and line/column bookkeeping
//// (1-based; column captured at token start). Dart throws CompileError with
//// phase 'lexer'; here scanning returns `Result` with a `LexError` the loader
//// maps onto the parse-stage StagedError.
////
//// Dart source of truth: glp_runtime/lib/compiler/{token,lexer}.dart

import gleam/float
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/string

/// Token types for GLP lexical analysis (token.dart TokenType, 1:1).
pub type TokenType {
  // Identifiers and literals
  TokAtom
  TokVariable
  TokReader
  TokNumber
  TokString
  // Delimiters
  LParen
  RParen
  LBracket
  RBracket
  LBrace
  RBrace
  // Punctuation
  Dot
  Comma
  Pipe
  Question
  Semicolon
  // Operators
  Implies
  Assign
  GuardSep
  // Arithmetic operators
  Plus
  Minus
  Star
  Slash
  SlashSlash
  Mod
  // Comparison operators
  Less
  Greater
  LessEqual
  GreaterEqual
  Equals
  ArithEqual
  ArithNotEqual
  GroundEqual
  Univ
  UnivDecompose
  // Special
  Underscore
  Tilde
  Hash
  Backslash
  At
  AtLess
  AtGreater
  AtLessEqual
  AtGreaterEqual
  // Type declarations
  ColonColonEq
  Procedure
  // End of file
  Eof
}

/// A literal payload (Dart `Object? literal` — NUMBER carries int/double,
/// STRING carries the string).
pub type Literal {
  LitInt(Int)
  LitReal(Float)
  LitString(String)
}

/// A lexical unit (token.dart Token).
pub type Token {
  Token(
    token_type: TokenType,
    lexeme: String,
    line: Int,
    column: Int,
    literal: Option(Literal),
  )
}

/// A lexer error (Dart CompileError with phase 'lexer').
pub type LexError {
  LexError(message: String, line: Int, column: Int)
}

/// Lexer state: remaining graphemes + 1-based position.
type Lx {
  Lx(rest: List(String), line: Int, column: Int)
}

/// Tokenize the entire source (lexer.dart `tokenize`). The token list always
/// ends with an `Eof` token.
pub fn tokenize(source: String) -> Result(List(Token), LexError) {
  scan_all(Lx(string.to_graphemes(source), 1, 1), [])
}

fn scan_all(lx: Lx, acc: List(Token)) -> Result(List(Token), LexError) {
  let lx = skip_whitespace_and_comments(lx)
  case lx.rest {
    [] ->
      Ok(list.reverse([Token(Eof, "", lx.line, lx.column, None), ..acc]))
    _ ->
      case scan_token(lx) {
        Ok(#(lx, token)) -> scan_all(lx, [token, ..acc])
        Error(e) -> Error(e)
      }
  }
}

/// Consume one grapheme (lexer.dart `_advance` — column is incremented on
/// every consume; newline line-bookkeeping is done by the callers that expect
/// newlines, mirroring the Dart).
fn advance(lx: Lx) -> #(Lx, String) {
  case lx.rest {
    [c, ..rest] -> #(Lx(rest, lx.line, lx.column + 1), c)
    [] -> #(lx, "\u{0000}")
  }
}

/// Consume `expected` if it is next (lexer.dart `_match`).
fn match(lx: Lx, expected: String) -> #(Lx, Bool) {
  case lx.rest {
    [c, ..rest] if c == expected -> #(Lx(rest, lx.line, lx.column + 1), True)
    _ -> #(lx, False)
  }
}

fn peek(lx: Lx) -> String {
  case lx.rest {
    [c, ..] -> c
    [] -> "\u{0000}"
  }
}

fn peek_next(lx: Lx) -> String {
  case lx.rest {
    [_, c, ..] -> c
    _ -> "\u{0000}"
  }
}

fn simple(lx: Lx, tt: TokenType, lexeme: String, line: Int, column: Int) -> Result(#(Lx, Token), LexError) {
  Ok(#(lx, Token(tt, lexeme, line, column, None)))
}

/// Scan a single token (lexer.dart `_scanToken`).
fn scan_token(lx: Lx) -> Result(#(Lx, Token), LexError) {
  let line = lx.line
  let column = lx.column
  let #(lx, c) = advance(lx)
  case c {
    "(" -> simple(lx, LParen, c, line, column)
    ")" -> simple(lx, RParen, c, line, column)
    "[" -> simple(lx, LBracket, c, line, column)
    "]" -> simple(lx, RBracket, c, line, column)
    "{" -> simple(lx, LBrace, c, line, column)
    "}" -> simple(lx, RBrace, c, line, column)
    "." ->
      // "..=" → UNIV_DECOMPOSE; ".." without "=" backs up to a single DOT
      // (Dart backs up _current; here we only commit when all three match).
      case lx.rest {
        [".", "=", ..rest] ->
          Ok(#(
            Lx(rest, lx.line, lx.column + 2),
            Token(UnivDecompose, "..=", line, column, None),
          ))
        _ -> simple(lx, Dot, c, line, column)
      }
    "," -> simple(lx, Comma, c, line, column)
    "?" -> simple(lx, Question, c, line, column)
    "|" -> simple(lx, Pipe, c, line, column)
    ";" -> simple(lx, Semicolon, c, line, column)
    "~" -> simple(lx, Tilde, c, line, column)
    "#" -> simple(lx, Hash, c, line, column)
    "\\" -> simple(lx, Backslash, c, line, column)
    "@" -> {
      // Standard-order term-comparison guards: @< @> @>= @=<. `@` followed by
      // anything else stays a bare AT — the Goal@Agent isolate-spawn operator.
      let #(lx1, lt) = match(lx, "<")
      case lt {
        True -> Ok(#(lx1, Token(AtLess, "@<", line, column, None)))
        False -> {
          let #(lx1, gt) = match(lx, ">")
          case gt {
            True -> {
              let #(lx2, ge) = match(lx1, "=")
              case ge {
                True -> Ok(#(lx2, Token(AtGreaterEqual, "@>=", line, column, None)))
                False -> Ok(#(lx1, Token(AtGreater, "@>", line, column, None)))
              }
            }
            False -> {
              let #(lx1, eq) = match(lx, "=")
              case eq {
                True -> {
                  let #(lx2, le) = match(lx1, "<")
                  case le {
                    True -> Ok(#(lx2, Token(AtLessEqual, "@=<", line, column, None)))
                    False ->
                      Error(LexError(
                        "Expected \"<\" after \"@=\" (the @=< guard)",
                        line,
                        column,
                      ))
                  }
                }
                False -> simple(lx, At, c, line, column)
              }
            }
          }
        }
      }
    }
    // Arithmetic operators
    "+" -> simple(lx, Plus, c, line, column)
    "*" -> simple(lx, Star, c, line, column)
    "/" -> {
      let #(lx1, slash) = match(lx, "/")
      case slash {
        True -> Ok(#(lx1, Token(SlashSlash, "//", line, column, None)))
        False -> simple(lx, Slash, c, line, column)
      }
    }
    // Comparison operators
    "<" -> simple(lx, Less, c, line, column)
    ">" -> {
      let #(lx1, eq) = match(lx, "=")
      case eq {
        True -> Ok(#(lx1, Token(GreaterEqual, ">=", line, column, None)))
        False -> simple(lx, Greater, c, line, column)
      }
    }
    "=" -> scan_equals(lx, line, column)
    ":" -> {
      let #(lx1, colon) = match(lx, ":")
      case colon {
        True -> {
          let #(lx2, eq) = match(lx1, "=")
          case eq {
            True -> Ok(#(lx2, Token(ColonColonEq, "::=", line, column, None)))
            False ->
              Error(LexError("Expected \"=\" after \"::\"", line, column))
          }
        }
        False -> {
          let #(lx1, dash) = match(lx, "-")
          case dash {
            True -> Ok(#(lx1, Token(Implies, ":-", line, column, None)))
            False -> {
              let #(lx1, eq) = match(lx, "=")
              case eq {
                True -> Ok(#(lx1, Token(Assign, ":=", line, column, None)))
                False -> Error(LexError("Unexpected character :", line, column))
              }
            }
          }
        }
      }
    }
    "_" ->
      case is_alpha_numeric(peek(lx)) {
        False -> simple(lx, Underscore, c, line, column)
        True -> identifier(lx, c, line, column)
      }
    "\"" -> scan_string(lx, "\"", line, column)
    "'" -> scan_string(lx, "'", line, column)
    "-" ->
      // Negative number literal, or minus operator.
      case is_digit(peek(lx)) {
        True -> number(lx, c, line, column)
        False -> simple(lx, Minus, c, line, column)
      }
    _ ->
      case is_digit(c) {
        True -> number(lx, c, line, column)
        False ->
          case is_alpha(c) {
            True -> identifier(lx, c, line, column)
            False ->
              Error(LexError("Unexpected character: " <> c, line, column))
          }
      }
  }
}

/// The '='-family: =.. =< =:= =\= =?= or plain '=' (lexer.dart case '=').
fn scan_equals(lx: Lx, line: Int, column: Int) -> Result(#(Lx, Token), LexError) {
  let #(lx1, dot) = match(lx, ".")
  case dot {
    True -> {
      let #(lx2, dot2) = match(lx1, ".")
      case dot2 {
        True -> Ok(#(lx2, Token(Univ, "=..", line, column, None)))
        False -> Error(LexError("Expected \"..\" after \"=.\"", line, column))
      }
    }
    False -> {
      let #(lx1, lt) = match(lx, "<")
      case lt {
        True -> Ok(#(lx1, Token(LessEqual, "=<", line, column, None)))
        False -> {
          let #(lx1, colon) = match(lx, ":")
          case colon {
            True -> {
              let #(lx2, eq) = match(lx1, "=")
              case eq {
                True -> Ok(#(lx2, Token(ArithEqual, "=:=", line, column, None)))
                False ->
                  Error(LexError("Expected \"=\" after \"=:\"", line, column))
              }
            }
            False -> {
              let #(lx1, bs) = match(lx, "\\")
              case bs {
                True -> {
                  let #(lx2, eq) = match(lx1, "=")
                  case eq {
                    True ->
                      Ok(#(lx2, Token(ArithNotEqual, "=\\=", line, column, None)))
                    False ->
                      Error(LexError("Expected \"=\" after \"=\\\"", line, column))
                  }
                }
                False -> {
                  let #(lx1, q) = match(lx, "?")
                  case q {
                    True -> {
                      let #(lx2, eq) = match(lx1, "=")
                      case eq {
                        True ->
                          Ok(#(lx2, Token(GroundEqual, "=?=", line, column, None)))
                        False ->
                          Error(LexError(
                            "Expected \"=\" after \"=?\"",
                            line,
                            column,
                          ))
                      }
                    }
                    False -> Ok(#(lx, Token(Equals, "=", line, column, None)))
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}

/// Scan identifier, keyword, variable, or reader (lexer.dart `_identifier`).
fn identifier(lx: Lx, first: String, line: Int, column: Int) -> Result(#(Lx, Token), LexError) {
  let #(lx, tail) = take_while(lx, [], is_alpha_numeric)
  let text = first <> string.concat(list.reverse(tail))
  // 'mod' keyword — but only when not followed by '(' (predicate call).
  case text == "mod" && peek(lx) != "(" {
    True -> Ok(#(lx, Token(Mod, text, line, column, None)))
    False ->
      case text == "procedure" {
        True -> Ok(#(lx, Token(Procedure, text, line, column, None)))
        False -> {
          // Variable: starts uppercase, or '_' followed by uppercase (named
          // anonymous variables like _Out are variables, not atoms).
          let is_variable = case string.to_graphemes(text) {
            ["_", second, ..] -> is_upper(second)
            [first, ..] -> is_upper(first)
            [] -> False
          }
          // Reader syntax: Variable? — consume the '?'.
          case peek(lx) == "?" && is_variable {
            True -> {
              let #(lx, _) = advance(lx)
              Ok(#(lx, Token(TokReader, text, line, column, None)))
            }
            False -> {
              let tt = case is_variable {
                True -> TokVariable
                False -> TokAtom
              }
              Ok(#(lx, Token(tt, text, line, column, None)))
            }
          }
        }
      }
  }
}

/// Scan a number — integer or real; a leading '-' was consumed by the caller
/// (lexer.dart `_number`).
fn number(lx: Lx, first: String, line: Int, column: Int) -> Result(#(Lx, Token), LexError) {
  let #(lx, digits) = take_while(lx, [], is_digit)
  let int_part = first <> string.concat(list.reverse(digits))
  // Decimal point only when followed by a digit ("3." stays NUMBER(3) DOT).
  let #(lx, text) = case peek(lx) == "." && is_digit(peek_next(lx)) {
    True -> {
      let #(lx, _) = advance(lx)
      let #(lx, frac) = take_while(lx, [], is_digit)
      #(lx, int_part <> "." <> string.concat(list.reverse(frac)))
    }
    False -> #(lx, int_part)
  }
  case string.contains(text, ".") {
    True -> {
      let assert Ok(value) = float.parse(text)
      Ok(#(lx, Token(TokNumber, text, line, column, Some(LitReal(value)))))
    }
    False -> {
      let assert Ok(value) = int.parse(text)
      Ok(#(lx, Token(TokNumber, text, line, column, Some(LitInt(value)))))
    }
  }
}

/// Scan a string literal or quoted atom (lexer.dart `_string`): single quotes
/// produce a quoted ATOM (usable as functor), double quotes a STRING literal.
fn scan_string(lx: Lx, quote: String, line: Int, column: Int) -> Result(#(Lx, Token), LexError) {
  case string_body(lx, quote, []) {
    Ok(#(lx, text)) ->
      case quote {
        "'" -> Ok(#(lx, Token(TokAtom, text, line, column, None)))
        _ -> Ok(#(lx, Token(TokString, text, line, column, Some(LitString(text)))))
      }
    Error(Nil) -> Error(LexError("Unterminated string", line, column))
  }
}

fn string_body(lx: Lx, quote: String, acc: List(String)) -> Result(#(Lx, String), Nil) {
  case lx.rest {
    [] -> Error(Nil)
    [c, ..] if c == quote -> {
      let #(lx, _) = advance(lx)
      Ok(#(lx, string.concat(list.reverse(acc))))
    }
    ["\\", ..] -> {
      let #(lx, _) = advance(lx)
      case lx.rest {
        [] -> Error(Nil)
        [esc, ..] -> {
          let translated = case esc {
            "n" -> "\n"
            "t" -> "\t"
            "r" -> "\r"
            _ -> esc
          }
          let #(lx, _) = advance(lx)
          string_body(lx, quote, [translated, ..acc])
        }
      }
    }
    ["\n", ..rest] ->
      // Newline inside a string: line++, column resets (Dart sets 0, then
      // advance makes it 1).
      string_body(Lx(rest, lx.line + 1, 1), quote, ["\n", ..acc])
    [c, ..] -> {
      let #(lx, _) = advance(lx)
      string_body(lx, quote, [c, ..acc])
    }
  }
}

/// Skip whitespace, % line comments, and /* */ block comments
/// (lexer.dart `_skipWhitespaceAndComments`).
fn skip_whitespace_and_comments(lx: Lx) -> Lx {
  case lx.rest {
    [" ", ..rest] | ["\t", ..rest] | ["\r", ..rest] ->
      skip_whitespace_and_comments(Lx(rest, lx.line, lx.column + 1))
    ["\n", ..rest] -> skip_whitespace_and_comments(Lx(rest, lx.line + 1, 1))
    ["%", ..] -> skip_whitespace_and_comments(skip_line_comment(lx))
    ["/", "*", ..rest] ->
      skip_whitespace_and_comments(skip_block_comment(Lx(
        rest,
        lx.line,
        lx.column + 2,
      )))
    _ -> lx
  }
}

fn skip_line_comment(lx: Lx) -> Lx {
  case lx.rest {
    [] -> lx
    ["\n", ..] -> lx
    [_, ..rest] -> skip_line_comment(Lx(rest, lx.line, lx.column + 1))
  }
}

fn skip_block_comment(lx: Lx) -> Lx {
  case lx.rest {
    [] -> lx
    ["*", "/", ..rest] -> Lx(rest, lx.line, lx.column + 2)
    ["\n", ..rest] -> skip_block_comment(Lx(rest, lx.line + 1, 1))
    [_, ..rest] -> skip_block_comment(Lx(rest, lx.line, lx.column + 1))
  }
}

fn take_while(lx: Lx, acc: List(String), keep: fn(String) -> Bool) -> #(Lx, List(String)) {
  case lx.rest {
    [c, ..rest] ->
      case keep(c) {
        True -> take_while(Lx(rest, lx.line, lx.column + 1), [c, ..acc], keep)
        False -> #(lx, acc)
      }
    [] -> #(lx, acc)
  }
}

fn is_digit(c: String) -> Bool {
  case c {
    "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" -> True
    _ -> False
  }
}

fn is_alpha(c: String) -> Bool {
  case c {
    "_" -> True
    _ -> is_upper(c) || is_lower(c)
  }
}

fn is_alpha_numeric(c: String) -> Bool {
  is_alpha(c) || is_digit(c)
}

/// First code point of a grapheme (Dart `codeUnitAt(0)` equivalent for the
/// ASCII classes the lexer tests).
fn code(c: String) -> Int {
  case string.to_utf_codepoints(c) {
    [cp, ..] -> string.utf_codepoint_to_int(cp)
    [] -> 0
  }
}

fn is_upper(c: String) -> Bool {
  let n = code(c)
  n >= 65 && n <= 90
}

fn is_lower(c: String) -> Bool {
  let n = code(c)
  n >= 97 && n <= 122
}
