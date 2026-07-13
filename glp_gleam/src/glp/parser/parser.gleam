//// glp/parser/parser — recursive-descent parser for GLP source (feature 050,
//// T014).
////
//// Hand-port (R1 — no parser generator) of the Dart parser; the Dart parser's
//// behaviour is the conformance oracle, including error messages. The entry
//// point is `parse_module` (Dart `Parser.parseModule`), producing the
//// `ast.SourceModule` consumed by the load pipeline. The Dart legacy
//// `parse()`/`Program` path (declaration-skipping) is not part of the
//// pipeline surface and is not ported; the never-called Dart helpers
//// `_parseGuard`, `_isTypeOrProcDeclaration` and `_checkContiguousClauses`
//// (legacy-path only) are likewise omitted.
////
//// Porting notes:
//// - Dart's mutable `_current` index with save/restore backtracking is
////   restructured as pure lookahead over the immutable remaining-token list
////   (`_current--` rewinds become peek-before-consume); net token consumption
////   and error behaviour are identical.
//// - Dart renders token types in error messages via `TokenType.X` enum
////   toString — reproduced by `token_type_name`.
//// - Dart `CompileError` with phase 'parser' maps onto `ParseError`; the
////   loader (T020) maps it onto the parse-stage `StagedError`.
////
//// Dart source of truth: glp_runtime/lib/compiler/parser.dart

import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/result
import gleam/string
import glp/analysis/prelude
import glp/analysis/type_ast.{
  type Pos, type ProcDecl, type TypeDef, type TypeExpr, Pos,
}
import glp/analysis/type_conversion
import glp/parser/ast.{type Term}
import glp/parser/lexer.{type Token, type TokenType, Token}
import glp/runtime/terms

/// A parser error (Dart CompileError with phase 'parser').
pub type ParseError {
  ParseError(message: String, line: Int, column: Int)
}

/// Parser state: the remaining tokens (always Eof-terminated by the lexer)
/// plus the pending clause carried between procedure groups (Dart
/// `_pendingClause` — a clause parsed with the same functor but a different
/// arity belongs to the next procedure).
type Ps {
  Ps(rest: List(Token), pending_clause: Option(ast.Clause))
}

// ── Token-stream helpers (Dart _peek/_advance/_check/_match/_consume) ──────

fn eof_token() -> Token {
  Token(lexer.Eof, "", 0, 0, None)
}

fn peek(ps: Ps) -> Token {
  case ps.rest {
    [t, ..] -> t
    [] -> eof_token()
  }
}

/// The token after the current one (Dart `tokens[_current + 1]`; the list is
/// Eof-terminated so the bounds check is implicit — Eof matches none of the
/// looked-for types).
fn peek2(ps: Ps) -> Token {
  case ps.rest {
    [_, t, ..] -> t
    _ -> eof_token()
  }
}

fn is_at_end(ps: Ps) -> Bool {
  peek(ps).token_type == lexer.Eof
}

/// Consume one token (never moves past Eof, mirroring Dart `_advance`).
fn advance(ps: Ps) -> #(Ps, Token) {
  case ps.rest {
    [Token(token_type: lexer.Eof, ..) as t, ..] -> #(ps, t)
    [t, ..rest] -> #(Ps(..ps, rest: rest), t)
    [] -> #(ps, eof_token())
  }
}

/// Dart `_check`: false at end of input, else type comparison.
fn check(ps: Ps, tt: TokenType) -> Bool {
  let t = peek(ps)
  t.token_type != lexer.Eof && t.token_type == tt
}

/// Dart `_match`: consume the token if it has the expected type.
fn match_tok(ps: Ps, tt: TokenType) -> #(Ps, Bool) {
  case check(ps, tt) {
    True -> {
      let #(ps, _) = advance(ps)
      #(ps, True)
    }
    False -> #(ps, False)
  }
}

/// Dart `_consume`: advance past the expected type or error at the current
/// token's position.
fn consume(
  ps: Ps,
  tt: TokenType,
  message: String,
) -> Result(#(Ps, Token), ParseError) {
  case check(ps, tt) {
    True -> Ok(advance(ps))
    False -> fail_at(message, peek(ps))
  }
}

fn fail_at(message: String, t: Token) -> Result(a, ParseError) {
  Error(ParseError(message, t.line, t.column))
}

fn tok_pos(t: Token) -> Pos {
  Pos(t.line, t.column)
}

/// The Dart `TokenType` enum rendering used inside error messages
/// (`'Expected term, got ${_peek().type}'` prints `TokenType.LBRACKET`).
fn token_type_name(tt: TokenType) -> String {
  case tt {
    lexer.TokAtom -> "TokenType.ATOM"
    lexer.TokVariable -> "TokenType.VARIABLE"
    lexer.TokReader -> "TokenType.READER"
    lexer.TokNumber -> "TokenType.NUMBER"
    lexer.TokString -> "TokenType.STRING"
    lexer.LParen -> "TokenType.LPAREN"
    lexer.RParen -> "TokenType.RPAREN"
    lexer.LBracket -> "TokenType.LBRACKET"
    lexer.RBracket -> "TokenType.RBRACKET"
    lexer.LBrace -> "TokenType.LBRACE"
    lexer.RBrace -> "TokenType.RBRACE"
    lexer.Dot -> "TokenType.DOT"
    lexer.Comma -> "TokenType.COMMA"
    lexer.Pipe -> "TokenType.PIPE"
    lexer.Question -> "TokenType.QUESTION"
    lexer.Semicolon -> "TokenType.SEMICOLON"
    lexer.Implies -> "TokenType.IMPLIES"
    lexer.Assign -> "TokenType.ASSIGN"
    lexer.GuardSep -> "TokenType.GUARD_SEP"
    lexer.Plus -> "TokenType.PLUS"
    lexer.Minus -> "TokenType.MINUS"
    lexer.Star -> "TokenType.STAR"
    lexer.Slash -> "TokenType.SLASH"
    lexer.SlashSlash -> "TokenType.SLASH_SLASH"
    lexer.Mod -> "TokenType.MOD"
    lexer.Less -> "TokenType.LESS"
    lexer.Greater -> "TokenType.GREATER"
    lexer.LessEqual -> "TokenType.LESS_EQUAL"
    lexer.GreaterEqual -> "TokenType.GREATER_EQUAL"
    lexer.Equals -> "TokenType.EQUALS"
    lexer.ArithEqual -> "TokenType.ARITH_EQUAL"
    lexer.ArithNotEqual -> "TokenType.ARITH_NOT_EQUAL"
    lexer.GroundEqual -> "TokenType.GROUND_EQUAL"
    lexer.Univ -> "TokenType.UNIV"
    lexer.UnivDecompose -> "TokenType.UNIV_DECOMPOSE"
    lexer.Underscore -> "TokenType.UNDERSCORE"
    lexer.Tilde -> "TokenType.TILDE"
    lexer.Hash -> "TokenType.HASH"
    lexer.Backslash -> "TokenType.BACKSLASH"
    lexer.At -> "TokenType.AT"
    lexer.AtLess -> "TokenType.AT_LESS"
    lexer.AtGreater -> "TokenType.AT_GREATER"
    lexer.AtLessEqual -> "TokenType.AT_LESS_EQUAL"
    lexer.AtGreaterEqual -> "TokenType.AT_GREATER_EQUAL"
    lexer.ColonColonEq -> "TokenType.COLONCOLONEQ"
    lexer.Procedure -> "TokenType.PROCEDURE"
    lexer.Eof -> "TokenType.EOF"
  }
}

/// `elem, elem, …` continuation: keep parsing elements while a comma follows
/// (the Dart `while (_match(COMMA))` shape). `acc` arrives reversed with the
/// already-parsed first element(s); the result is in source order.
fn comma_list(
  ps: Ps,
  acc: List(a),
  parse_elem: fn(Ps) -> Result(#(Ps, a), ParseError),
) -> Result(#(Ps, List(a)), ParseError) {
  let #(ps1, matched) = match_tok(ps, lexer.Comma)
  case matched {
    False -> Ok(#(ps, list.reverse(acc)))
    True -> {
      use #(ps2, elem) <- result.try(parse_elem(ps1))
      comma_list(ps2, [elem, ..acc], parse_elem)
    }
  }
}

/// `(arg, …)` argument list with the opening paren already consumed; empty
/// `()` allowed (Dart `if (!_check(RPAREN)) {…} _consume(RPAREN, msg)`).
fn parse_args_until_rparen(
  ps: Ps,
  parse_arg: fn(Ps) -> Result(#(Ps, a), ParseError),
  rparen_message: String,
) -> Result(#(Ps, List(a)), ParseError) {
  case check(ps, lexer.RParen) {
    True -> {
      let #(ps, _) = advance(ps)
      Ok(#(ps, []))
    }
    False -> {
      use #(ps, first) <- result.try(parse_arg(ps))
      use #(ps, args) <- result.try(comma_list(ps, [first], parse_arg))
      use #(ps, _) <- result.try(consume(ps, lexer.RParen, rparen_message))
      Ok(#(ps, args))
    }
  }
}

/// Optional `(arg, …)` — no parens means no arguments.
fn parse_optional_paren_args(
  ps: Ps,
  parse_arg: fn(Ps) -> Result(#(Ps, a), ParseError),
  rparen_message: String,
) -> Result(#(Ps, List(a)), ParseError) {
  let #(ps1, has) = match_tok(ps, lexer.LParen)
  case has {
    False -> Ok(#(ps, []))
    True -> parse_args_until_rparen(ps1, parse_arg, rparen_message)
  }
}

// ── Module entry point (Dart parseModule) ──────────────────────────────────

/// Accumulators for the parseModule main loop (all lists reversed while
/// accumulating).
type ModuleAcc {
  ModuleAcc(
    type_defs: List(TypeDef),
    proc_decls: List(ProcDecl),
    procedures: List(ast.Procedure),
    pending_proc_decl: Option(ProcDecl),
    seen: Dict(String, ast.Procedure),
  )
}

/// Parse tokens into a `SourceModule` (Dart `parseModule`). The parser never
/// fills `param_proc_decls` — parameterized-template separation happens in the
/// type-check stage (Dart parser likewise constructs `Module` with the
/// default empty `paramProcDecls`).
pub fn parse_module(
  tokens: List(Token),
) -> Result(ast.SourceModule, ParseError) {
  let ps = Ps(tokens, None)
  use #(ps, declaration, compile_mode) <- result.try(parse_declarations(
    ps,
    None,
    ast.User,
  ))
  use acc <- result.try(module_loop(
    ps,
    ModuleAcc([], [], [], None, dict.new()),
  ))
  Ok(ast.SourceModule(
    declaration: declaration,
    type_defs: list.reverse(acc.type_defs),
    proc_declarations: list.reverse(acc.proc_decls),
    param_proc_decls: [],
    procedures: list.reverse(acc.procedures),
    compile_mode: compile_mode,
    pos: Pos(1, 1),
  ))
}

/// Declarations at the start of the file: `-module(name).`, `-mode(m).`,
/// deprecated `-stdlib.`, removed `-export`/`-import`. An unknown `-atom`
/// (or `-` not followed by an atom) ends the declaration section without
/// consuming anything (Dart backs `_current` up and breaks).
fn parse_declarations(
  ps: Ps,
  declaration: Option(ast.ModuleDeclaration),
  compile_mode: ast.CompileMode,
) -> Result(
  #(Ps, Option(ast.ModuleDeclaration), ast.CompileMode),
  ParseError,
) {
  case !is_at_end(ps) && check(ps, lexer.Minus) {
    False -> Ok(#(ps, declaration, compile_mode))
    True -> {
      let start = peek(ps)
      let #(ps1, _) = advance(ps)
      case check(ps1, lexer.TokAtom) {
        // Back up, not a declaration
        False -> Ok(#(ps, declaration, compile_mode))
        True -> {
          let #(ps2, keyword) = advance(ps1)
          case keyword.lexeme {
            "module" -> {
              use #(ps3, _) <- result.try(consume(
                ps2,
                lexer.LParen,
                "Expected \"(\" after module",
              ))
              use #(ps4, name) <- result.try(parse_module_name(ps3))
              use #(ps5, _) <- result.try(consume(
                ps4,
                lexer.RParen,
                "Expected \")\" after module name",
              ))
              use #(ps6, _) <- result.try(consume(
                ps5,
                lexer.Dot,
                "Expected \".\" after module declaration",
              ))
              parse_declarations(
                ps6,
                Some(ast.ModuleDeclaration(name, tok_pos(start))),
                compile_mode,
              )
            }
            // -stdlib. is deprecated — treated as -mode(system).
            "stdlib" -> {
              use #(ps3, _) <- result.try(consume(
                ps2,
                lexer.Dot,
                "Expected \".\" after stdlib declaration",
              ))
              parse_declarations(ps3, declaration, ast.System)
            }
            "mode" -> {
              use #(ps3, _) <- result.try(consume(
                ps2,
                lexer.LParen,
                "Expected \"(\" after mode",
              ))
              case check(ps3, lexer.TokAtom) {
                False ->
                  fail_at(
                    "Expected \"user\" or \"system\" in mode declaration",
                    peek(ps3),
                  )
                True -> {
                  let #(ps4, mode_token) = advance(ps3)
                  use new_mode <- result.try(case mode_token.lexeme {
                    "user" -> Ok(ast.User)
                    "system" -> Ok(ast.System)
                    other ->
                      fail_at(
                        "Invalid mode \""
                          <> other
                          <> "\". Expected \"user\" or \"system\".",
                        mode_token,
                      )
                  })
                  use #(ps5, _) <- result.try(consume(
                    ps4,
                    lexer.RParen,
                    "Expected \")\" after mode",
                  ))
                  use #(ps6, _) <- result.try(consume(
                    ps5,
                    lexer.Dot,
                    "Expected \".\" after mode declaration",
                  ))
                  parse_declarations(ps6, declaration, new_mode)
                }
              }
            }
            "export" ->
              fail_at(
                "The -export() declaration is no longer supported. Use 'exported procedure' instead.",
                start,
              )
            "import" ->
              fail_at(
                "The -import() declaration is no longer supported. Use 'imported procedure' instead.",
                start,
              )
            // Unknown declaration, back up to the '-'
            _ -> Ok(#(ps, declaration, compile_mode))
          }
        }
      }
    }
  }
}

/// Hierarchical module name `utils.list` (Dart `_parseModuleName`; the
/// Dart consume-then-rewind of a trailing DOT is a lookahead-2 here).
fn parse_module_name(ps: Ps) -> Result(#(Ps, String), ParseError) {
  use #(ps, first) <- result.try(consume(
    ps,
    lexer.TokAtom,
    "Expected module name",
  ))
  module_name_loop(ps, [first.lexeme])
}

fn module_name_loop(
  ps: Ps,
  parts: List(String),
) -> Result(#(Ps, String), ParseError) {
  case ps.rest {
    [
      Token(token_type: lexer.Dot, ..),
      Token(token_type: lexer.TokAtom, ..) as atom_tok,
      ..rest
    ] -> module_name_loop(Ps(..ps, rest: rest), [atom_tok.lexeme, ..parts])
    _ -> Ok(#(ps, parts |> list.reverse |> string.join(".")))
  }
}

fn no_clauses_message(pending: ProcDecl) -> String {
  "Procedure declaration for \""
  <> pending.name
  <> "\" has no clauses.\n  A procedure declaration must be immediately followed by its clauses."
}

/// The parseModule main loop: procedure declarations, type definitions, and
/// clause groups in order, with pending-declaration and contiguity checks.
fn module_loop(ps: Ps, acc: ModuleAcc) -> Result(ModuleAcc, ParseError) {
  case is_at_end(ps) {
    True ->
      // Check for dangling procedure declaration at end of file
      case acc.pending_proc_decl {
        Some(pending) -> {
          let pending_sig = type_ast.key(pending)
          case
            !prelude.is_builtin_procedure(pending_sig) && !pending.imported
          {
            True ->
              Error(ParseError(
                no_clauses_message(pending),
                pending.pos.line,
                pending.pos.column,
              ))
            False -> Ok(acc)
          }
        }
        None -> Ok(acc)
      }
    False -> {
      // 'procedure …' or 'exported procedure …' or 'imported procedure …'
      let is_procedure_decl =
        check(ps, lexer.Procedure)
        || {
          check(ps, lexer.TokAtom)
          && { peek(ps).lexeme == "exported" || peek(ps).lexeme == "imported" }
          && peek2(ps).token_type == lexer.Procedure
        }
      case is_procedure_decl {
        True -> {
          use acc <- result.try(clear_pending_before_decl(acc))
          use #(ps, decl) <- result.try(parse_proc_declaration(ps))
          // Imported procedures are declaration-only — no clauses expected
          let pending = case decl.imported {
            True -> None
            False -> Some(decl)
          }
          module_loop(
            ps,
            ModuleAcc(
              ..acc,
              proc_decls: [decl, ..acc.proc_decls],
              pending_proc_decl: pending,
            ),
          )
        }
        False ->
          case check(ps, lexer.TokVariable) || check(ps, lexer.TokReader) {
            True ->
              // Type definition (has ::=) or a clause head
              case is_type_definition(ps) {
                True -> {
                  use acc <- result.try(clear_pending_before_typedef(
                    acc,
                    peek(ps),
                  ))
                  use #(ps, type_def) <- result.try(parse_type_def(ps))
                  module_loop(
                    ps,
                    ModuleAcc(..acc, type_defs: [type_def, ..acc.type_defs]),
                  )
                }
                False -> {
                  use #(ps, acc) <- result.try(parse_procedure_into(ps, acc))
                  module_loop(ps, acc)
                }
              }
            False ->
              case check(ps, lexer.TokAtom) {
                True -> {
                  use #(ps, acc) <- result.try(parse_procedure_into(ps, acc))
                  module_loop(ps, acc)
                }
                False ->
                  fail_at("Unexpected token: " <> peek(ps).lexeme, peek(ps))
              }
          }
      }
    }
  }
}

/// Pending-declaration check on reaching another procedure declaration:
/// builtins and imported declarations need no clauses; anything else does.
fn clear_pending_before_decl(acc: ModuleAcc) -> Result(ModuleAcc, ParseError) {
  case acc.pending_proc_decl {
    None -> Ok(acc)
    Some(pending) -> {
      let pending_sig = type_ast.key(pending)
      case !prelude.is_builtin_procedure(pending_sig) && !pending.imported {
        True ->
          Error(ParseError(
            no_clauses_message(pending),
            pending.pos.line,
            pending.pos.column,
          ))
        False -> Ok(ModuleAcc(..acc, pending_proc_decl: None))
      }
    }
  }
}

/// Pending-declaration check on reaching a type definition.
fn clear_pending_before_typedef(
  acc: ModuleAcc,
  token: Token,
) -> Result(ModuleAcc, ParseError) {
  case acc.pending_proc_decl {
    None -> Ok(acc)
    Some(pending) -> {
      let pending_sig = type_ast.key(pending)
      case !prelude.is_builtin_procedure(pending_sig) && !pending.imported {
        True ->
          fail_at(
            "Type definition cannot appear between procedure declaration and its clauses.\n  Procedure \""
              <> pending.name
              <> "\" declared at line "
              <> int.to_string(pending.pos.line)
              <> " needs clauses.",
            token,
          )
        False -> Ok(ModuleAcc(..acc, pending_proc_decl: None))
      }
    }
  }
}

/// Parse one procedure group and fold it into the module accumulators:
/// pending-declaration match, then the non-contiguous-clauses check.
fn parse_procedure_into(
  ps: Ps,
  acc: ModuleAcc,
) -> Result(#(Ps, ModuleAcc), ParseError) {
  use #(ps, procedure) <- result.try(parse_procedure(ps))
  let sig = ast.signature(procedure)
  use acc <- result.try(case acc.pending_proc_decl {
    None -> Ok(acc)
    Some(pending) -> {
      let pending_sig = type_ast.key(pending)
      case sig == pending_sig {
        // This clause matches the pending declaration - good
        True -> Ok(ModuleAcc(..acc, pending_proc_decl: None))
        False ->
          case prelude.is_builtin_procedure(pending_sig) {
            // Pending was a builtin (no clauses needed) - clear it
            True -> Ok(ModuleAcc(..acc, pending_proc_decl: None))
            False ->
              Error(ParseError(
                "Clause for \""
                  <> sig
                  <> "\" appears between procedure declaration and clauses for \""
                  <> pending_sig
                  <> "\".\n  Procedure declaration at line "
                  <> int.to_string(pending.pos.line)
                  <> " must be immediately followed by its clauses.",
                procedure.pos.line,
                procedure.pos.column,
              ))
          }
      }
    }
  })
  case dict.get(acc.seen, sig) {
    Ok(first) ->
      Error(ParseError(
        "Non-contiguous clauses for \""
          <> sig
          <> "\".\n  First group at line "
          <> int.to_string(first.pos.line)
          <> ", second group at line "
          <> int.to_string(procedure.pos.line)
          <> ".\n  All clauses for a predicate must be together in the source file.",
        procedure.pos.line,
        procedure.pos.column,
      ))
    Error(_) ->
      Ok(#(
        ps,
        ModuleAcc(
          ..acc,
          procedures: [procedure, ..acc.procedures],
          seen: dict.insert(acc.seen, sig, procedure),
        ),
      ))
  }
}

// ── Procedures and clauses ──────────────────────────────────────────────────

/// One or more clauses with the same head functor/arity (Dart
/// `_parseProcedure`); a same-functor different-arity clause becomes the
/// pending clause for the next group.
fn parse_procedure(ps: Ps) -> Result(#(Ps, ast.Procedure), ParseError) {
  use #(ps, first_clause) <- result.try(case ps.pending_clause {
    Some(clause) -> Ok(#(Ps(..ps, pending_clause: None), clause))
    None -> parse_clause(ps)
  })
  let name = first_clause.head.functor
  let arity = ast.atom_arity(first_clause.head)
  use #(ps, clauses) <- result.try(procedure_clause_loop(ps, name, arity, [
    first_clause,
  ]))
  Ok(#(ps, ast.Procedure(name, arity, list.reverse(clauses), first_clause.pos)))
}

fn procedure_clause_loop(
  ps: Ps,
  name: String,
  arity: Int,
  acc: List(ast.Clause),
) -> Result(#(Ps, List(ast.Clause)), ParseError) {
  case !is_at_end(ps) && could_be_same_procedure(ps, name) {
    False -> Ok(#(ps, acc))
    True -> {
      use #(ps, clause) <- result.try(parse_clause(ps))
      let clause_arity = ast.atom_arity(clause.head)
      case clause.head.functor == name && clause_arity != arity {
        // Different arity - a different procedure; store as pending and break
        True -> Ok(#(Ps(..ps, pending_clause: Some(clause)), acc))
        False ->
          case clause.head.functor != name {
            True ->
              Error(ParseError(
                "Clause for "
                  <> clause.head.functor
                  <> "/"
                  <> int.to_string(clause_arity)
                  <> " found, expected "
                  <> name
                  <> "/"
                  <> int.to_string(arity),
                clause.pos.line,
                clause.pos.column,
              ))
            False -> procedure_clause_loop(ps, name, arity, [clause, ..acc])
          }
      }
    }
  }
}

/// Could the next clause belong to the current procedure? Same predicate
/// name, or (for the operator-headed procedures `:=`, `=..`, `..=`, `=`)
/// a variable/reader/underscore followed by the operator token.
fn could_be_same_procedure(ps: Ps, name: String) -> Bool {
  let t = peek(ps)
  case t.token_type == lexer.TokAtom && t.lexeme == name {
    True -> True
    False -> {
      let var_like =
        t.token_type == lexer.TokVariable
        || t.token_type == lexer.TokReader
        || t.token_type == lexer.Underscore
      let next = peek2(ps).token_type
      case name {
        ":=" -> var_like && next == lexer.Assign
        "=.." -> var_like && next == lexer.Univ
        "..=" -> var_like && next == lexer.UnivDecompose
        "=" -> var_like && next == lexer.Equals
        _ -> False
      }
    }
  }
}

/// Clause: `Head :- Guards | Body.` or `Head :- Body.` or `Head.`
/// (Dart `_parseClause`). Everything between `:-` and `|` parses as
/// goal-or-guard; a `|` retroactively converts those into guards.
fn parse_clause(ps: Ps) -> Result(#(Ps, ast.Clause), ParseError) {
  use #(ps, head) <- result.try(parse_atom(ps))
  let #(ps, has_implies) = match_tok(ps, lexer.Implies)
  use #(ps, guards, body) <- result.try(case has_implies {
    False -> Ok(#(ps, None, None))
    True -> {
      use #(ps, first) <- result.try(parse_goal_or_guard(ps))
      use #(ps, predicates) <- result.try(comma_list(
        ps,
        [first],
        parse_goal_or_guard,
      ))
      let #(ps, has_pipe) = match_tok(ps, lexer.Pipe)
      case has_pipe {
        True -> {
          // Everything before | were guards - convert Goal to Guard
          let guards = list.map(predicates, goal_to_guard)
          use #(ps, first_goal) <- result.try(parse_goal(ps))
          use #(ps, body) <- result.try(comma_list(ps, [first_goal], parse_goal))
          Ok(#(ps, Some(guards), Some(body)))
        }
        // No | separator, so everything was body goals
        False -> Ok(#(ps, None, Some(predicates)))
      }
    }
  })
  use #(ps, _) <- result.try(consume(
    ps,
    lexer.Dot,
    "Expected \".\" at end of clause",
  ))
  Ok(#(ps, ast.Clause(head, guards, body, head.pos)))
}

/// Goal → Guard conversion over the uniform functor/args view; a leading `~`
/// on the functor marks a negated guard.
fn goal_to_guard(goal: ast.Goal) -> ast.Guard {
  let functor = ast.goal_functor(goal)
  let negated = string.starts_with(functor, "~")
  let actual_functor = case negated {
    True -> string.drop_start(functor, 1)
    False -> functor
  }
  ast.Guard(actual_functor, ast.goal_args(goal), negated, ast.goal_pos(goal))
}

// ── Goal-or-guard (the pre-`|` predicate parser) ────────────────────────────

/// A predicate that could be either a guard or a goal (Dart
/// `_parseGoalOrGuard`).
fn parse_goal_or_guard(ps: Ps) -> Result(#(Ps, ast.Goal), ParseError) {
  // Check for guard negation: ~G
  let neg_token = peek(ps)
  let #(ps, negated) = match_tok(ps, lexer.Tilde)
  let neg_pos = tok_pos(neg_token)
  // Check for double negation ~~G (syntactically forbidden)
  case negated && check(ps, lexer.Tilde) {
    True -> fail_at("Double negation ~~G is not allowed", peek(ps))
    False ->
      case check(ps, lexer.LParen) {
        True -> gog_paren(ps, negated, neg_pos)
        False -> gog_after_negation(ps, negated, neg_pos)
      }
  }
}

/// Parenthesized `(Goal)` or disjunction `(Goal1 ; Goal2)`.
fn gog_paren(
  ps: Ps,
  negated: Bool,
  neg_pos: Pos,
) -> Result(#(Ps, ast.Goal), ParseError) {
  let #(ps, start_token) = advance(ps)
  use #(ps, first_goal) <- result.try(parse_goal_or_guard(ps))
  let #(ps, has_semicolon) = match_tok(ps, lexer.Semicolon)
  case has_semicolon {
    True ->
      // Disjunction - negation not allowed
      case negated {
        True ->
          Error(ParseError(
            "Guard negation (~) cannot be applied to disjunction",
            neg_pos.line,
            neg_pos.column,
          ))
        False -> {
          use #(ps, second_goal) <- result.try(parse_goal_or_guard(ps))
          use #(ps, _) <- result.try(consume(
            ps,
            lexer.RParen,
            "Expected \")\" after disjunction",
          ))
          // ';'(Goal1, Goal2) over the term-encoded goals
          Ok(#(
            ps,
            ast.Goal(
              ";",
              [ast.goal_to_term(first_goal), ast.goal_to_term(second_goal)],
              tok_pos(start_token),
            ),
          ))
        }
      }
    False -> {
      use #(ps, _) <- result.try(consume(
        ps,
        lexer.RParen,
        "Expected \")\" after guard",
      ))
      case negated {
        True ->
          Ok(#(
            ps,
            ast.Goal(
              "~" <> ast.goal_functor(first_goal),
              ast.goal_args(first_goal),
              neg_pos,
            ),
          ))
        False -> Ok(#(ps, first_goal))
      }
    }
  }
}

/// The var-lookahead, atom, and infix-comparison alternatives of
/// `_parseGoalOrGuard` (after the negation and paren checks).
fn gog_after_negation(
  ps: Ps,
  negated: Bool,
  neg_pos: Pos,
) -> Result(#(Ps, ast.Goal), ParseError) {
  let var_like = check(ps, lexer.TokVariable) || check(ps, lexer.TokReader)
  let next = peek2(ps).token_type
  case var_like {
    True ->
      case next {
        lexer.Assign -> {
          let #(ps, var_token) = advance(ps)
          let #(ps, _) = advance(ps)
          let var_term = var_term_of(var_token)
          use #(ps, expr) <- result.try(parse_expression(ps, 0))
          Ok(#(ps, ast.Goal(":=", [var_term, expr], tok_pos(var_token))))
        }
        lexer.Univ -> {
          let #(ps, var_token) = advance(ps)
          let #(ps, _) = advance(ps)
          let var_term = var_term_of(var_token)
          use #(ps, expr) <- result.try(parse_term(ps))
          Ok(#(ps, ast.Goal("=..", [var_term, expr], tok_pos(var_token))))
        }
        lexer.UnivDecompose -> {
          let #(ps, var_token) = advance(ps)
          let #(ps, _) = advance(ps)
          let var_term = var_term_of(var_token)
          use #(ps, expr) <- result.try(parse_term(ps))
          Ok(#(ps, ast.Goal("..=", [var_term, expr], tok_pos(var_token))))
        }
        lexer.Equals -> {
          let #(ps, var_token) = advance(ps)
          let #(ps, _) = advance(ps)
          let var_term = var_term_of(var_token)
          use #(ps, term) <- result.try(parse_term(ps))
          Ok(#(ps, ast.Goal("=", [var_term, term], tok_pos(var_token))))
        }
        lexer.Hash -> {
          // Dynamic remote goal: Var # Goal - negation not allowed
          let #(ps, var_token) = advance(ps)
          let #(ps, _) = advance(ps)
          case negated {
            True ->
              Error(ParseError(
                "Guard negation (~) cannot be applied to remote goal",
                neg_pos.line,
                neg_pos.column,
              ))
            False -> {
              use #(ps, inner_goal) <- result.try(parse_goal(ps))
              Ok(#(
                ps,
                ast.RemoteGoal(
                  var_term_of(var_token),
                  inner_goal,
                  tok_pos(var_token),
                ),
              ))
            }
          }
        }
        _ -> gog_comparison_fallback(ps, negated, neg_pos)
      }
    False ->
      case check(ps, lexer.TokAtom) {
        True -> gog_atom(ps, negated, neg_pos)
        False -> gog_comparison_fallback(ps, negated, neg_pos)
      }
  }
}

/// Regular predicate `functor(args…)`, static remote goal `atom # Goal`,
/// unification `atom(…) = Term`, or spawn `Goal@AgentId`.
fn gog_atom(
  ps: Ps,
  negated: Bool,
  neg_pos: Pos,
) -> Result(#(Ps, ast.Goal), ParseError) {
  use #(ps, functor_token) <- result.try(consume(
    ps,
    lexer.TokAtom,
    "Expected predicate name",
  ))
  use #(ps, args) <- result.try(parse_optional_paren_args(
    ps,
    parse_term,
    "Expected \")\" after arguments",
  ))
  let #(ps_hash, has_hash) = match_tok(ps, lexer.Hash)
  case has_hash {
    True ->
      // Static remote goal: atom # goal
      case args {
        [_, ..] ->
          fail_at(
            "Module name cannot have arguments: " <> functor_token.lexeme,
            functor_token,
          )
        [] ->
          case negated {
            True ->
              Error(ParseError(
                "Guard negation (~) cannot be applied to remote goal",
                neg_pos.line,
                neg_pos.column,
              ))
            False -> {
              use #(ps, inner_goal) <- result.try(parse_goal(ps_hash))
              Ok(#(
                ps,
                ast.RemoteGoal(
                  ast.ConstTerm(
                    terms.ConstAtom(functor_token.lexeme),
                    tok_pos(functor_token),
                  ),
                  inner_goal,
                  tok_pos(functor_token),
                ),
              ))
            }
          }
      }
    False -> {
      let #(ps_eq, has_equals) = match_tok(ps, lexer.Equals)
      case has_equals {
        True -> {
          // foo = bar, or foo(a) = X
          let left_term = case args {
            [] ->
              ast.ConstTerm(
                terms.ConstAtom(functor_token.lexeme),
                tok_pos(functor_token),
              )
            _ ->
              ast.StructTerm(functor_token.lexeme, args, tok_pos(functor_token))
          }
          use #(ps, right_term) <- result.try(parse_term(ps_eq))
          case negated {
            True ->
              Error(ParseError(
                "Guard negation (~) cannot be applied to unification",
                neg_pos.line,
                neg_pos.column,
              ))
            False ->
              Ok(#(
                ps,
                ast.Goal(
                  "=",
                  [left_term, right_term],
                  tok_pos(functor_token),
                ),
              ))
          }
        }
        False -> {
          // ~functor convention if negated (detected during Guard conversion)
          let functor = case negated {
            True -> "~" <> functor_token.lexeme
            False -> functor_token.lexeme
          }
          let goal_pos = case negated {
            True -> neg_pos
            False -> tok_pos(functor_token)
          }
          // Check for spawn annotation: Goal@AgentId
          let #(ps_at, has_at) = match_tok(ps, lexer.At)
          case has_at {
            True -> {
              use #(ps, agent_token) <- result.try(consume(
                ps_at,
                lexer.TokAtom,
                "Expected agent identifier after @",
              ))
              Ok(#(
                ps,
                ast.SpawnGoal(
                  ast.InnerGoal(functor, args, goal_pos),
                  agent_token.lexeme,
                  tok_pos(functor_token),
                ),
              ))
            }
            False -> Ok(#(ps, ast.Goal(functor, args, goal_pos)))
          }
        }
      }
    }
  }
}

/// Infix comparison fallback (e.g. `X < Y`, `X? mod P? =:= 0`): arithmetic
/// parses at precedence 6 so comparison operators stay unconsumed, then one
/// comparison operator becomes the goal functor.
fn gog_comparison_fallback(
  ps: Ps,
  negated: Bool,
  neg_pos: Pos,
) -> Result(#(Ps, ast.Goal), ParseError) {
  use #(ps, left) <- result.try(parse_expression(ps, 6))
  case is_comparison_operator(peek(ps).token_type) {
    True -> {
      let #(ps, op_token) = advance(ps)
      use #(ps, right) <- result.try(parse_expression(ps, 6))
      // Transform infix to prefix: X < Y → <(X, Y); ~ uses the ~op functor
      let functor = case negated {
        True -> "~" <> op_token.lexeme
        False -> op_token.lexeme
      }
      let goal_pos = case negated {
        True -> neg_pos
        False -> tok_pos(op_token)
      }
      Ok(#(ps, ast.Goal(functor, [left, right], goal_pos)))
    }
    False -> fail_at("Expected predicate name or comparison", peek(ps))
  }
}

/// Comparison operators accepted as infix guard/goal functors, including the
/// standard-order term-comparison guards `@< @> @=< @>=` (T011/FR-037).
fn is_comparison_operator(tt: TokenType) -> Bool {
  case tt {
    lexer.Less
    | lexer.Greater
    | lexer.LessEqual
    | lexer.GreaterEqual
    | lexer.Equals
    | lexer.ArithEqual
    | lexer.ArithNotEqual
    | lexer.GroundEqual
    | lexer.AtLess
    | lexer.AtGreater
    | lexer.AtLessEqual
    | lexer.AtGreaterEqual -> True
    _ -> False
  }
}

fn var_term_of(var_token: Token) -> Term {
  ast.VarTerm(
    var_token.lexeme,
    var_token.token_type == lexer.TokReader,
    tok_pos(var_token),
  )
}

// ── Clause heads ────────────────────────────────────────────────────────────

/// Clause head (Dart `_parseAtom`): `functor(args…)`, or the operator heads
/// `Var := Expr`, `Var =.. Expr`, `Var ..= Expr`, `Var = Term` (also with a
/// parsed-atom left side for `foo(a,b) =.. L` and `foo(a) = X`).
fn parse_atom(ps: Ps) -> Result(#(Ps, ast.Atom), ParseError) {
  let t = peek(ps)
  let var_like =
    t.token_type == lexer.TokVariable
    || t.token_type == lexer.TokReader
    || t.token_type == lexer.Underscore
  case var_like {
    // Dart consumes the variable and rewinds when no operator follows; here
    // the operator is looked ahead before consuming.
    True ->
      case peek2(ps).token_type {
        lexer.Assign -> {
          let #(ps, var_token) = advance(ps)
          let #(ps, _) = advance(ps)
          // ':='(Var, Expr) or ':='(_, Expr)
          let lhs_term = case var_token.token_type {
            lexer.Underscore -> ast.UnderscoreTerm(False, tok_pos(var_token))
            _ -> var_term_of(var_token)
          }
          use #(ps, expr) <- result.try(parse_term(ps))
          Ok(#(ps, ast.Atom(":=", [lhs_term, expr], tok_pos(var_token))))
        }
        lexer.Univ -> {
          let #(ps, var_token) = advance(ps)
          let #(ps, _) = advance(ps)
          use #(ps, expr) <- result.try(parse_term(ps))
          Ok(#(
            ps,
            ast.Atom("=..", [var_term_of(var_token), expr], tok_pos(var_token)),
          ))
        }
        lexer.UnivDecompose -> {
          let #(ps, var_token) = advance(ps)
          let #(ps, _) = advance(ps)
          use #(ps, expr) <- result.try(parse_term(ps))
          Ok(#(
            ps,
            ast.Atom("..=", [var_term_of(var_token), expr], tok_pos(var_token)),
          ))
        }
        lexer.Equals -> {
          let #(ps, var_token) = advance(ps)
          let #(ps, _) = advance(ps)
          use #(ps, term) <- result.try(parse_term(ps))
          Ok(#(
            ps,
            ast.Atom("=", [var_term_of(var_token), term], tok_pos(var_token)),
          ))
        }
        _ -> parse_atom_functor(ps)
      }
    False -> parse_atom_functor(ps)
  }
}

fn parse_atom_functor(ps: Ps) -> Result(#(Ps, ast.Atom), ParseError) {
  use #(ps, functor_token) <- result.try(consume(
    ps,
    lexer.TokAtom,
    "Expected predicate name",
  ))
  use #(ps, args) <- result.try(parse_optional_paren_args(
    ps,
    parse_term,
    "Expected \")\" after arguments",
  ))
  // foo(a,b) =.. L
  let #(ps_univ, has_univ) = match_tok(ps, lexer.Univ)
  case has_univ {
    True -> {
      let left_term =
        ast.StructTerm(functor_token.lexeme, args, tok_pos(functor_token))
      use #(ps, right_term) <- result.try(parse_term(ps_univ))
      Ok(#(
        ps,
        ast.Atom("=..", [left_term, right_term], tok_pos(functor_token)),
      ))
    }
    False -> {
      // foo = bar, foo(a) = X
      let #(ps_eq, has_equals) = match_tok(ps, lexer.Equals)
      case has_equals {
        True -> {
          let left_term = case args {
            [] ->
              ast.ConstTerm(
                terms.ConstAtom(functor_token.lexeme),
                tok_pos(functor_token),
              )
            _ ->
              ast.StructTerm(functor_token.lexeme, args, tok_pos(functor_token))
          }
          use #(ps, right_term) <- result.try(parse_term(ps_eq))
          Ok(#(
            ps,
            ast.Atom("=", [left_term, right_term], tok_pos(functor_token)),
          ))
        }
        False ->
          Ok(#(
            ps,
            ast.Atom(functor_token.lexeme, args, tok_pos(functor_token)),
          ))
      }
    }
  }
}

// ── Body goals ──────────────────────────────────────────────────────────────

/// Body goal (Dart `_parseGoal`): predicate call, assignment/univ forms,
/// remote goal `Module # Goal` (static or dynamic), or spawn `Goal@AgentId`.
fn parse_goal(ps: Ps) -> Result(#(Ps, ast.Goal), ParseError) {
  case check(ps, lexer.TokVariable) || check(ps, lexer.TokReader) {
    True -> {
      let #(ps, var_token) = advance(ps)
      // Dynamic remote goal: Var # Goal
      let #(ps_hash, has_hash) = match_tok(ps, lexer.Hash)
      case has_hash {
        True -> {
          use #(ps, inner_goal) <- result.try(parse_goal(ps_hash))
          Ok(#(
            ps,
            ast.RemoteGoal(
              var_term_of(var_token),
              inner_goal,
              tok_pos(var_token),
            ),
          ))
        }
        False -> {
          let #(ps_assign, has_assign) = match_tok(ps, lexer.Assign)
          case has_assign {
            True -> {
              use #(ps, expr) <- result.try(parse_term(ps_assign))
              Ok(#(
                ps,
                ast.Goal(
                  ":=",
                  [var_term_of(var_token), expr],
                  tok_pos(var_token),
                ),
              ))
            }
            False -> {
              let #(ps_univ, has_univ) = match_tok(ps, lexer.Univ)
              case has_univ {
                True -> {
                  use #(ps, expr) <- result.try(parse_term(ps_univ))
                  Ok(#(
                    ps,
                    ast.Goal(
                      "=..",
                      [var_term_of(var_token), expr],
                      tok_pos(var_token),
                    ),
                  ))
                }
                False -> {
                  let #(ps_dec, has_decompose) =
                    match_tok(ps, lexer.UnivDecompose)
                  case has_decompose {
                    True -> {
                      use #(ps, expr) <- result.try(parse_term(ps_dec))
                      Ok(#(
                        ps,
                        ast.Goal(
                          "..=",
                          [var_term_of(var_token), expr],
                          tok_pos(var_token),
                        ),
                      ))
                    }
                    False -> {
                      let #(ps_eq, has_equals) = match_tok(ps, lexer.Equals)
                      case has_equals {
                        True -> {
                          use #(ps, term) <- result.try(parse_term(ps_eq))
                          Ok(#(
                            ps,
                            ast.Goal(
                              "=",
                              [var_term_of(var_token), term],
                              tok_pos(var_token),
                            ),
                          ))
                        }
                        False ->
                          fail_at(
                            "Expected predicate name or assignment, got variable \""
                              <> var_token.lexeme
                              <> "\"",
                            var_token,
                          )
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
    False -> {
      use #(ps, functor_token) <- result.try(consume(
        ps,
        lexer.TokAtom,
        "Expected predicate name",
      ))
      use #(ps, args) <- result.try(parse_optional_paren_args(
        ps,
        parse_term,
        "Expected \")\" after arguments",
      ))
      // Static remote goal: Module # Goal
      let #(ps_hash, has_hash) = match_tok(ps, lexer.Hash)
      case has_hash {
        True ->
          case args {
            [_, ..] ->
              fail_at(
                "Module name cannot have arguments: " <> functor_token.lexeme,
                functor_token,
              )
            [] -> {
              use #(ps, inner_goal) <- result.try(parse_goal(ps_hash))
              Ok(#(
                ps,
                ast.RemoteGoal(
                  ast.ConstTerm(
                    terms.ConstAtom(functor_token.lexeme),
                    tok_pos(functor_token),
                  ),
                  inner_goal,
                  tok_pos(functor_token),
                ),
              ))
            }
          }
        False -> {
          // foo(a,b) =.. L
          let #(ps_univ, has_univ) = match_tok(ps, lexer.Univ)
          case has_univ {
            True -> {
              let left_term =
                ast.StructTerm(
                  functor_token.lexeme,
                  args,
                  tok_pos(functor_token),
                )
              use #(ps, right_term) <- result.try(parse_term(ps_univ))
              Ok(#(
                ps,
                ast.Goal(
                  "=..",
                  [left_term, right_term],
                  tok_pos(functor_token),
                ),
              ))
            }
            False -> {
              // Check for spawn annotation: Goal@AgentId
              let #(ps_at, has_at) = match_tok(ps, lexer.At)
              case has_at {
                True -> {
                  use #(ps, agent_token) <- result.try(consume(
                    ps_at,
                    lexer.TokAtom,
                    "Expected agent identifier after @",
                  ))
                  Ok(#(
                    ps,
                    ast.SpawnGoal(
                      ast.InnerGoal(
                        functor_token.lexeme,
                        args,
                        tok_pos(functor_token),
                      ),
                      agent_token.lexeme,
                      tok_pos(functor_token),
                    ),
                  ))
                }
                False ->
                  Ok(#(
                    ps,
                    ast.Goal(
                      functor_token.lexeme,
                      args,
                      tok_pos(functor_token),
                    ),
                  ))
              }
            }
          }
        }
      }
    }
  }
}

// ── Terms and expressions (Pratt parsing) ───────────────────────────────────

/// Term: variable, structure, list, constant, underscore, tuple, or
/// expression (Dart `_parseTerm` — delegates to expression parsing).
fn parse_term(ps: Ps) -> Result(#(Ps, Term), ParseError) {
  parse_expression(ps, 0)
}

/// Expression parsing with precedence (Dart `_parseExpression`).
fn parse_expression(
  ps: Ps,
  min_precedence: Int,
) -> Result(#(Ps, Term), ParseError) {
  use #(ps, left) <- result.try(parse_primary(ps))
  expression_loop(ps, left, min_precedence)
}

fn expression_loop(
  ps: Ps,
  left: Term,
  min_precedence: Int,
) -> Result(#(Ps, Term), ParseError) {
  let t = peek(ps)
  case is_operator(t.token_type) && precedence(t.token_type) >= min_precedence {
    True -> {
      let #(ps, op) = advance(ps)
      use #(ps, right) <- result.try(parse_expression(
        ps,
        precedence(op.token_type) + 1,
      ))
      expression_loop(
        ps,
        ast.StructTerm(operator_functor(op), [left, right], tok_pos(op)),
        min_precedence,
      )
    }
    False -> Ok(#(ps, left))
  }
}

/// Primary expression (Dart `_parsePrimary`).
fn parse_primary(ps: Ps) -> Result(#(Ps, Term), ParseError) {
  let t = peek(ps)
  // Operator as functor (for type definitions like Exp ::= +(Exp?, Exp?)).
  // Must check BEFORE unary minus so -(X,Y) is parsed as struct, not neg.
  case
    is_arith_operator_token(t.token_type)
    && peek2(ps).token_type == lexer.LParen
  {
    True -> {
      let #(ps, functor_token) = advance(ps)
      let #(ps, _) = advance(ps)
      use #(ps, args) <- result.try(parse_args_until_rparen(
        ps,
        fn(ps) { parse_expression(ps, 0) },
        "Expected \")\" after operator struct arguments",
      ))
      Ok(#(
        ps,
        ast.StructTerm(functor_token.lexeme, args, tok_pos(functor_token)),
      ))
    }
    False -> {
      // Unary minus: -X becomes neg(X)
      let #(ps_minus, has_minus) = match_tok(ps, lexer.Minus)
      case has_minus {
        True -> {
          use #(ps, operand) <- result.try(parse_primary(ps_minus))
          Ok(#(ps, ast.StructTerm("neg", [operand], tok_pos(t))))
        }
        False -> parse_primary_rest(ps)
      }
    }
  }
}

fn parse_primary_rest(ps: Ps) -> Result(#(Ps, Term), ParseError) {
  case check(ps, lexer.TokVariable) || check(ps, lexer.TokReader) {
    True -> {
      let #(ps, token) = advance(ps)
      // Check for := assignment (Var := Expr)
      let #(ps_assign, has_assign) = match_tok(ps, lexer.Assign)
      case has_assign {
        True -> {
          use #(ps, expr) <- result.try(parse_expression(ps_assign, 0))
          Ok(#(
            ps,
            ast.StructTerm(":=", [var_term_of(token), expr], tok_pos(token)),
          ))
        }
        False -> Ok(#(ps, var_term_of(token)))
      }
    }
    False -> {
      // The underscore token is peek(ps) before the match consumes it.
      let underscore_token = peek(ps)
      let #(ps_us, has_underscore) = match_tok(ps, lexer.Underscore)
      case has_underscore {
        True -> {
          let #(ps, is_reader) = match_tok(ps_us, lexer.Question)
          Ok(#(ps, ast.UnderscoreTerm(is_reader, tok_pos(underscore_token))))
        }
        False ->
          case check(ps, lexer.TokNumber) {
            True -> {
              let #(ps, token) = advance(ps)
              case check(ps, lexer.Question) {
                True ->
                  fail_at(
                    "Reader mark \"?\" can only be applied to variables, not numbers",
                    peek(ps),
                  )
                False -> Ok(#(ps, number_const_term(token)))
              }
            }
            False ->
              case check(ps, lexer.TokString) {
                True -> {
                  let #(ps, token) = advance(ps)
                  case check(ps, lexer.Question) {
                    True ->
                      fail_at(
                        "Reader mark \"?\" can only be applied to variables, not strings",
                        peek(ps),
                      )
                    False -> Ok(#(ps, string_const_term(token)))
                  }
                }
                False ->
                  case check(ps, lexer.LBracket) {
                    True -> parse_list(ps)
                    False -> parse_primary_paren_or_atom(ps)
                  }
              }
          }
      }
    }
  }
}

fn number_const_term(token: Token) -> Term {
  let assert Some(literal) = token.literal
  let value = case literal {
    lexer.LitInt(i) -> terms.ConstInt(i)
    lexer.LitReal(r) -> terms.ConstReal(r)
    lexer.LitString(s) -> terms.ConstString(s)
  }
  ast.ConstTerm(value, tok_pos(token))
}

/// A string literal constant. The Dart parser wraps the value in quotes so
/// the type checker can tell strings from atoms; the Gleam port carries the
/// distinction in the typed `terms.ConstString` (T008 conformance note).
fn string_const_term(token: Token) -> Term {
  let assert Some(lexer.LitString(s)) = token.literal
  ast.ConstTerm(terms.ConstString(s), tok_pos(token))
}

fn parse_primary_paren_or_atom(ps: Ps) -> Result(#(Ps, Term), ParseError) {
  let #(ps_paren, has_paren) = match_tok(ps, lexer.LParen)
  case has_paren {
    // Parenthesized expression - tuple (A, B), single term (A), or arithmetic
    True -> {
      let start_token = peek(ps)
      use #(ps, first) <- result.try(parse_expression(ps_paren, 0))
      let #(ps_comma, has_comma) = match_tok(ps, lexer.Comma)
      case has_comma {
        True -> {
          use #(ps, second) <- result.try(parse_expression(ps_comma, 0))
          use #(ps, rest) <- result.try(comma_list(ps, [second], fn(ps) {
            parse_expression(ps, 0)
          }))
          use #(ps, _) <- result.try(consume(
            ps,
            lexer.RParen,
            "Expected \")\" after tuple",
          ))
          // Right-associative tuple: (A, B, C) = ','(A, ','(B, C))
          Ok(#(ps, build_tuple([first, ..rest], tok_pos(start_token))))
        }
        False -> {
          use #(ps, _) <- result.try(consume(
            ps,
            lexer.RParen,
            "Expected \")\" after expression",
          ))
          Ok(#(ps, first))
        }
      }
    }
    False ->
      case check(ps, lexer.TokAtom) {
        True -> {
          let #(ps, functor_token) = advance(ps)
          let #(ps_args, has_args) = match_tok(ps, lexer.LParen)
          case has_args {
            True -> {
              use #(ps, args) <- result.try(parse_args_until_rparen(
                ps_args,
                fn(ps) { parse_expression(ps, 0) },
                "Expected \")\" after structure arguments",
              ))
              case check(ps, lexer.Question) {
                True ->
                  fail_at(
                    "Reader mark \"?\" can only be applied to variables, not structures like "
                      <> functor_token.lexeme
                      <> "(...)",
                    peek(ps),
                  )
                False ->
                  Ok(#(
                    ps,
                    ast.StructTerm(
                      functor_token.lexeme,
                      args,
                      tok_pos(functor_token),
                    ),
                  ))
              }
            }
            False ->
              case check(ps, lexer.Question) {
                True ->
                  fail_at(
                    "Reader mark \"?\" can only be applied to variables, not constants like \""
                      <> functor_token.lexeme
                      <> "\"",
                    peek(ps),
                  )
                False ->
                  Ok(#(
                    ps,
                    ast.ConstTerm(
                      terms.ConstAtom(functor_token.lexeme),
                      tok_pos(functor_token),
                    ),
                  ))
              }
          }
        }
        False ->
          fail_at(
            "Expected term, got " <> token_type_name(peek(ps).token_type),
            peek(ps),
          )
      }
  }
}

fn build_tuple(terms_list: List(Term), pos: Pos) -> Term {
  case terms_list {
    [only] -> only
    [first, ..rest] -> ast.StructTerm(",", [first, build_tuple(rest, pos)], pos)
    [] -> panic as "parser: build_tuple on empty list is unreachable"
  }
}

/// Arithmetic-operator tokens usable as struct functors.
fn is_arith_operator_token(tt: TokenType) -> Bool {
  case tt {
    lexer.Plus
    | lexer.Minus
    | lexer.Star
    | lexer.Slash
    | lexer.SlashSlash
    | lexer.Mod -> True
    _ -> False
  }
}

/// Expression operators: arithmetic, comparison, `#` (module), `\`
/// (difference list) — Dart `_isOperator`.
fn is_operator(tt: TokenType) -> Bool {
  case tt {
    lexer.Plus
    | lexer.Minus
    | lexer.Star
    | lexer.Slash
    | lexer.SlashSlash
    | lexer.Mod
    | lexer.Less
    | lexer.Greater
    | lexer.LessEqual
    | lexer.GreaterEqual
    | lexer.Equals
    | lexer.ArithEqual
    | lexer.ArithNotEqual
    | lexer.Hash
    | lexer.Backslash -> True
    _ -> False
  }
}

/// Operator precedence (Dart `_precedence`).
fn precedence(tt: TokenType) -> Int {
  case tt {
    // Multiplicative
    lexer.Star | lexer.Slash | lexer.SlashSlash | lexer.Mod -> 20
    // Additive
    lexer.Plus | lexer.Minus -> 10
    // Module operator (very low, so M # foo(X,Y) parses correctly)
    lexer.Hash -> 2
    // Difference list operator (lowest, so [H|T]\T parses correctly)
    lexer.Backslash -> 1
    // Comparison (lower than arithmetic)
    lexer.Less
    | lexer.Greater
    | lexer.LessEqual
    | lexer.GreaterEqual
    | lexer.Equals
    | lexer.ArithEqual
    | lexer.ArithNotEqual -> 5
    _ -> 0
  }
}

/// Operator functor name for the AST (Dart `_operatorFunctor`; only called on
/// `is_operator` tokens, all of which are covered).
fn operator_functor(op: Token) -> String {
  case op.token_type {
    lexer.Plus -> "+"
    lexer.Minus -> "-"
    lexer.Star -> "*"
    lexer.Slash -> "/"
    lexer.SlashSlash -> "//"
    lexer.Mod -> "mod"
    lexer.Less -> "<"
    lexer.Greater -> ">"
    lexer.LessEqual -> "=<"
    lexer.GreaterEqual -> ">="
    lexer.Equals -> "="
    lexer.ArithEqual -> "=:="
    lexer.ArithNotEqual -> "=\\="
    lexer.Hash -> "#"
    lexer.Backslash -> "\\"
    _ -> panic as "parser: operator_functor on non-operator token"
  }
}

// ── Lists ───────────────────────────────────────────────────────────────────

/// List: `[]`, `[H|T]`, `[X]`, `[X,Y,Z]`, `[X,Y,Z|T]` (Dart `_parseList`).
fn parse_list(ps: Ps) -> Result(#(Ps, Term), ParseError) {
  use #(ps, bracket_token) <- result.try(consume(
    ps,
    lexer.LBracket,
    "Expected \"[\"",
  ))
  let bracket_pos = tok_pos(bracket_token)
  let #(ps_nil, is_empty) = match_tok(ps, lexer.RBracket)
  case is_empty {
    True -> {
      use _ <- result.try(reject_reader_mark_on_list(ps_nil))
      Ok(#(ps_nil, ast.ListTerm(None, None, bracket_pos)))
    }
    False -> {
      use #(ps, first) <- result.try(parse_term(ps))
      use #(ps, elements) <- result.try(comma_list(ps, [first], parse_term))
      let #(ps_pipe, has_pipe) = match_tok(ps, lexer.Pipe)
      case has_pipe {
        True -> {
          use #(ps, tail) <- result.try(parse_term(ps_pipe))
          use #(ps, _) <- result.try(consume(
            ps,
            lexer.RBracket,
            "Expected \"]\" after list tail",
          ))
          use _ <- result.try(reject_reader_mark_on_list(ps))
          Ok(#(ps, build_list(elements, tail, bracket_pos)))
        }
        False -> {
          use #(ps, _) <- result.try(consume(
            ps,
            lexer.RBracket,
            "Expected \"]\" after list elements",
          ))
          use _ <- result.try(reject_reader_mark_on_list(ps))
          Ok(#(
            ps,
            build_list(elements, ast.ListTerm(None, None, bracket_pos), bracket_pos),
          ))
        }
      }
    }
  }
}

/// Right-associative list build: `[X,Y,Z|T]` → `[X|[Y|[Z|T]]]`.
fn build_list(elements: List(Term), tail: Term, pos: Pos) -> Term {
  list.fold_right(elements, tail, fn(acc, element) {
    ast.ListTerm(Some(element), Some(acc), pos)
  })
}

fn reject_reader_mark_on_list(ps: Ps) -> Result(Nil, ParseError) {
  case check(ps, lexer.Question) {
    True ->
      fail_at(
        "Reader mark \"?\" can only be applied to variables, not lists",
        peek(ps),
      )
    False -> Ok(Nil)
  }
}

// ── Yardeni-Shapiro type declarations ───────────────────────────────────────

/// Is the upcoming input a type definition `TypeName ::= …` (also
/// parameterized `TypeName(X, Y) ::= …`)? Pure lookahead — distinguishes
/// type definitions from clause heads starting with a capitalized variable
/// (Dart `_isTypeDefinition`).
fn is_type_definition(ps: Ps) -> Bool {
  case ps.rest {
    [Token(token_type: lexer.TokVariable, ..), ..rest]
    | [Token(token_type: lexer.TokReader, ..), ..rest] -> {
      let after = case rest {
        [Token(token_type: lexer.LParen, ..), ..inner] ->
          skip_balanced_parens(inner, 1)
        _ -> rest
      }
      case after {
        [Token(token_type: lexer.ColonColonEq, ..), ..] -> True
        _ -> False
      }
    }
    _ -> False
  }
}

/// Skip past balanced parens (depth starts at 1 with the '(' consumed),
/// stopping at Eof like the Dart `while (!_isAtEnd() && depth > 0)` loop.
fn skip_balanced_parens(rest: List(Token), depth: Int) -> List(Token) {
  case depth {
    0 -> rest
    _ ->
      case rest {
        [] -> []
        [Token(token_type: lexer.Eof, ..), ..] -> rest
        [Token(token_type: lexer.LParen, ..), ..tail] ->
          skip_balanced_parens(tail, depth + 1)
        [Token(token_type: lexer.RParen, ..), ..tail] ->
          skip_balanced_parens(tail, depth - 1)
        [_, ..tail] -> skip_balanced_parens(tail, depth)
      }
  }
}

/// Type definition `TypeName ::= alt ; alt ; alt.` — also parameterized
/// `TypeName(X, Y) ::= …` and explicit duals `TypeName? ::= …` (Dart
/// `_parseTypeDef`).
fn parse_type_def(ps: Ps) -> Result(#(Ps, TypeDef), ParseError) {
  use #(ps, name_token) <- result.try(case check(ps, lexer.TokReader) {
    True -> Ok(advance(ps))
    False -> consume(ps, lexer.TokVariable, "Expected type name")
  })
  // For READER tokens (e.g., Channel?), append '?' to the name — explicit
  // dual type definitions.
  let type_name = case name_token.token_type {
    lexer.TokReader -> name_token.lexeme <> "?"
    _ -> name_token.lexeme
  }
  let #(ps_params, has_params) = match_tok(ps, lexer.LParen)
  use #(ps, type_params) <- result.try(case has_params {
    False -> Ok(#(ps, []))
    True -> {
      use #(ps, first) <- result.try(consume(
        ps_params,
        lexer.TokVariable,
        "Expected type parameter name",
      ))
      use #(ps, params) <- result.try(comma_list(
        ps,
        [first.lexeme],
        fn(ps) {
          use #(ps, param) <- result.try(consume(
            ps,
            lexer.TokVariable,
            "Expected type parameter name",
          ))
          Ok(#(ps, param.lexeme))
        },
      ))
      use #(ps, _) <- result.try(consume(
        ps,
        lexer.RParen,
        "Expected \")\" after type parameters",
      ))
      Ok(#(ps, params))
    }
  })
  use #(ps, _) <- result.try(consume(
    ps,
    lexer.ColonColonEq,
    "Expected \"::=\" in type definition",
  ))
  use #(ps, first_alt) <- result.try(parse_type_alt(ps))
  use #(ps, alternatives) <- result.try(semicolon_alt_loop(ps, [first_alt]))
  use #(ps, _) <- result.try(consume(
    ps,
    lexer.Dot,
    "Expected \".\" after type definition",
  ))
  Ok(#(
    ps,
    type_ast.TypeDef(
      name: type_name,
      type_params: type_params,
      alternatives: alternatives,
      pos: tok_pos(name_token),
    ),
  ))
}

fn semicolon_alt_loop(
  ps: Ps,
  acc: List(TypeExpr),
) -> Result(#(Ps, List(TypeExpr)), ParseError) {
  let #(ps_semi, has_semi) = match_tok(ps, lexer.Semicolon)
  case has_semi {
    False -> Ok(#(ps, list.reverse(acc)))
    True -> {
      use #(ps, alt) <- result.try(parse_type_alt(ps_semi))
      semicolon_alt_loop(ps, [alt, ..acc])
    }
  }
}

/// One type alternative: parse as Term, convert to TypeExpr (Dart
/// `_parseTypeAlt` per type-conversion.md).
fn parse_type_alt(ps: Ps) -> Result(#(Ps, TypeExpr), ParseError) {
  use #(ps, term) <- result.try(parse_type_alt_expression(ps, 0))
  Ok(#(ps, type_conversion.term_to_type_expr(term)))
}

/// Expression in type-alternative context — handles operators like `\` for
/// difference lists, and consumes a trailing `?` on the whole expression
/// (explicit duals) (Dart `_parseTypeAltExpression`).
fn parse_type_alt_expression(
  ps: Ps,
  min_precedence: Int,
) -> Result(#(Ps, Term), ParseError) {
  use #(ps, left) <- result.try(parse_type_alt_primary(ps))
  use #(ps, left) <- result.try(type_alt_expression_loop(
    ps,
    left,
    min_precedence,
  ))
  // A trailing `?` marks the whole alternative as a reader (explicit dual). Apply it to `left`
  // instead of discarding it, so a compound alternative like a difference-list `A\B?` is not
  // parsed identically to `A\B`, dropping the mode before type conversion (codexreview
  // 20260713T111017Z).
  let #(ps, has_question) = match_tok(ps, lexer.Question)
  let left = case has_question {
    True -> apply_type_alt_reader(left)
    False -> left
  }
  Ok(#(ps, left))
}

/// Apply a trailing reader `?` to a type-alternative term, encoded per Term variant the way the
/// primary parser and `type_conversion` already expect: `VarTerm`/`UnderscoreTerm` carry an
/// `is_reader` flag; a `StructTerm` (including the difference-list `\` and operator structs) encodes
/// it as a trailing `?` on the functor. `ListTerm`/`ConstTerm` carry no reader annotation in the AST
/// (the primary list parser likewise drops a trailing `?` on a list), so they are returned as-is.
fn apply_type_alt_reader(term: Term) -> Term {
  case term {
    ast.VarTerm(name, _, pos) -> ast.VarTerm(name, True, pos)
    ast.UnderscoreTerm(_, pos) -> ast.UnderscoreTerm(True, pos)
    ast.StructTerm(functor, args, pos) ->
      case string.ends_with(functor, "?") {
        True -> term
        False -> ast.StructTerm(functor <> "?", args, pos)
      }
    _ -> term
  }
}

fn type_alt_expression_loop(
  ps: Ps,
  left: Term,
  min_precedence: Int,
) -> Result(#(Ps, Term), ParseError) {
  let t = peek(ps)
  case is_operator(t.token_type) && precedence(t.token_type) >= min_precedence {
    True -> {
      let #(ps, op) = advance(ps)
      use #(ps, right) <- result.try(parse_type_alt_expression(
        ps,
        precedence(op.token_type) + 1,
      ))
      type_alt_expression_loop(
        ps,
        ast.StructTerm(operator_functor(op), [left, right], tok_pos(op)),
        min_precedence,
      )
    }
    False -> Ok(#(ps, left))
  }
}

/// Primary term in type-alternative context — allows trailing `?` on
/// structures/lists/parens for explicit dual definitions (Dart
/// `_parseTypeAltPrimary`).
fn parse_type_alt_primary(ps: Ps) -> Result(#(Ps, Term), ParseError) {
  let t = peek(ps)
  // Operator as functor (Exp ::= +(Exp?, Exp?))
  case
    is_arith_operator_token(t.token_type)
    && peek2(ps).token_type == lexer.LParen
  {
    True -> {
      let #(ps, functor_token) = advance(ps)
      let #(ps, _) = advance(ps)
      use #(ps, args) <- result.try(parse_args_until_rparen(
        ps,
        fn(ps) { parse_type_alt_expression(ps, 0) },
        "Expected \")\" after operator struct arguments",
      ))
      let #(ps, _) = match_tok(ps, lexer.Question)
      Ok(#(
        ps,
        ast.StructTerm(functor_token.lexeme, args, tok_pos(functor_token)),
      ))
    }
    False -> parse_type_alt_primary_rest(ps)
  }
}

fn parse_type_alt_primary_rest(ps: Ps) -> Result(#(Ps, Term), ParseError) {
  let var_like = check(ps, lexer.TokVariable) || check(ps, lexer.TokReader)
  case var_like && peek2(ps).token_type == lexer.LParen {
    // Parameterized type reference TypeName(Arg1, …) — uppercase functor
    // with '(' is a type ref, never a struct; reader mode is encoded in the
    // functor name ("Stream?") for type_conversion to decode.
    True -> {
      let #(ps, token) = advance(ps)
      let #(ps, _) = advance(ps)
      use #(ps, args) <- result.try(parse_args_until_rparen(
        ps,
        fn(ps) { parse_type_alt_expression(ps, 0) },
        "Expected \")\" after type arguments",
      ))
      let #(ps, trailing_q) = match_tok(ps, lexer.Question)
      let is_reader = token.token_type == lexer.TokReader
      let effective_name = case is_reader || trailing_q {
        True -> token.lexeme <> "?"
        False -> token.lexeme
      }
      Ok(#(ps, ast.StructTerm(effective_name, args, tok_pos(token))))
    }
    False ->
      case var_like {
        // Variable or Reader (simple, non-parameterized)
        True -> {
          let #(ps, token) = advance(ps)
          Ok(#(ps, var_term_of(token)))
        }
        False -> {
          let #(ps_us, has_underscore) = match_tok(ps, lexer.Underscore)
          case has_underscore {
            True -> {
              let token = peek(ps)
              let #(ps, is_reader) = match_tok(ps_us, lexer.Question)
              Ok(#(ps, ast.UnderscoreTerm(is_reader, tok_pos(token))))
            }
            False ->
              case check(ps, lexer.TokNumber) {
                True -> {
                  let #(ps, token) = advance(ps)
                  Ok(#(ps, number_const_term(token)))
                }
                False ->
                  case check(ps, lexer.TokString) {
                    True -> {
                      let #(ps, token) = advance(ps)
                      Ok(#(ps, string_const_term(token)))
                    }
                    False ->
                      case check(ps, lexer.LBracket) {
                        True -> parse_type_alt_list(ps)
                        False -> parse_type_alt_paren_or_atom(ps)
                      }
                  }
              }
          }
        }
      }
  }
}

fn parse_type_alt_paren_or_atom(ps: Ps) -> Result(#(Ps, Term), ParseError) {
  let #(ps_paren, has_paren) = match_tok(ps, lexer.LParen)
  case has_paren {
    True -> {
      let start_token = peek(ps)
      use #(ps, first) <- result.try(parse_type_alt_expression(ps_paren, 0))
      let #(ps_comma, has_comma) = match_tok(ps, lexer.Comma)
      case has_comma {
        True -> {
          use #(ps, second) <- result.try(parse_type_alt_expression(
            ps_comma,
            0,
          ))
          use #(ps, rest) <- result.try(comma_list(ps, [second], fn(ps) {
            parse_type_alt_expression(ps, 0)
          }))
          use #(ps, _) <- result.try(consume(
            ps,
            lexer.RParen,
            "Expected \")\" after tuple",
          ))
          // Allow trailing ? on parenthesized expression
          let #(ps, _) = match_tok(ps, lexer.Question)
          Ok(#(ps, build_tuple([first, ..rest], tok_pos(start_token))))
        }
        False -> {
          use #(ps, _) <- result.try(consume(
            ps,
            lexer.RParen,
            "Expected \")\" after expression",
          ))
          let #(ps, _) = match_tok(ps, lexer.Question)
          Ok(#(ps, first))
        }
      }
    }
    False ->
      case check(ps, lexer.TokAtom) {
        True -> {
          let #(ps, functor_token) = advance(ps)
          let #(ps_args, has_args) = match_tok(ps, lexer.LParen)
          case has_args {
            True -> {
              use #(ps, args) <- result.try(parse_args_until_rparen(
                ps_args,
                fn(ps) { parse_type_alt_expression(ps, 0) },
                "Expected \")\" after structure arguments",
              ))
              // Allow trailing ? on structure (explicit duals)
              let #(ps, _) = match_tok(ps, lexer.Question)
              Ok(#(
                ps,
                ast.StructTerm(
                  functor_token.lexeme,
                  args,
                  tok_pos(functor_token),
                ),
              ))
            }
            False ->
              Ok(#(
                ps,
                ast.ConstTerm(
                  terms.ConstAtom(functor_token.lexeme),
                  tok_pos(functor_token),
                ),
              ))
          }
        }
        False ->
          fail_at(
            "Expected type alternative term, got "
              <> token_type_name(peek(ps).token_type),
            peek(ps),
          )
      }
  }
}

/// List in type-alternative context — allows trailing `?` (Dart
/// `_parseTypeAltList`).
fn parse_type_alt_list(ps: Ps) -> Result(#(Ps, Term), ParseError) {
  use #(ps, bracket_token) <- result.try(consume(
    ps,
    lexer.LBracket,
    "Expected \"[\"",
  ))
  let bracket_pos = tok_pos(bracket_token)
  let #(ps_nil, is_empty) = match_tok(ps, lexer.RBracket)
  case is_empty {
    True -> {
      let #(ps, _) = match_tok(ps_nil, lexer.Question)
      Ok(#(ps, ast.ListTerm(None, None, bracket_pos)))
    }
    False -> {
      use #(ps, first) <- result.try(parse_type_alt_expression(ps, 0))
      use #(ps, elements) <- result.try(comma_list(ps, [first], fn(ps) {
        parse_type_alt_expression(ps, 0)
      }))
      let #(ps_pipe, has_pipe) = match_tok(ps, lexer.Pipe)
      case has_pipe {
        True -> {
          use #(ps, tail) <- result.try(parse_type_alt_expression(ps_pipe, 0))
          use #(ps, _) <- result.try(consume(
            ps,
            lexer.RBracket,
            "Expected \"]\" after list tail",
          ))
          let #(ps, _) = match_tok(ps, lexer.Question)
          Ok(#(ps, build_list(elements, tail, bracket_pos)))
        }
        False -> {
          use #(ps, _) <- result.try(consume(
            ps,
            lexer.RBracket,
            "Expected \"]\" after list elements",
          ))
          let #(ps, _) = match_tok(ps, lexer.Question)
          Ok(#(
            ps,
            build_list(
              elements,
              ast.ListTerm(None, None, bracket_pos),
              bracket_pos,
            ),
          ))
        }
      }
    }
  }
}

// ── Procedure declarations ──────────────────────────────────────────────────

/// Procedure declaration: `procedure name(Type?, Type).`, `exported
/// procedure …`, `imported procedure [path#]name(…)` (Dart
/// `_parseProcDeclaration`). Parens optional for nullary procedures.
fn parse_proc_declaration(ps: Ps) -> Result(#(Ps, ProcDecl), ParseError) {
  let start = peek(ps)
  let #(ps, exported, imported) = case
    check(ps, lexer.TokAtom) && peek(ps).lexeme == "exported"
  {
    True -> {
      let #(ps, _) = advance(ps)
      #(ps, True, False)
    }
    False ->
      case check(ps, lexer.TokAtom) && peek(ps).lexeme == "imported" {
        True -> {
          let #(ps, _) = advance(ps)
          #(ps, False, True)
        }
        False -> #(ps, False, False)
      }
  }
  use #(ps, _) <- result.try(consume(
    ps,
    lexer.Procedure,
    "Expected \"procedure\" keyword",
  ))
  use #(ps, name_token) <- result.try(parse_proc_name_token(ps))
  // For imported procedures, parse the #-separated module path; the last
  // component is the procedure name.
  use #(ps, name, module_path) <- result.try(case imported {
    True -> parse_imported_name_path(ps, name_token.lexeme)
    False -> Ok(#(ps, name_token.lexeme, None))
  })
  let #(ps_args, has_paren) = match_tok(ps, lexer.LParen)
  use #(ps, arg_types) <- result.try(case has_paren {
    False -> Ok(#(ps, []))
    True ->
      parse_args_until_rparen(
        ps_args,
        parse_proc_arg_type,
        "Expected \")\" after procedure arguments",
      )
  })
  use #(ps, _) <- result.try(consume(
    ps,
    lexer.Dot,
    "Expected \".\" after procedure declaration",
  ))
  Ok(#(
    ps,
    type_ast.ProcDecl(
      name: name,
      arg_types: arg_types,
      type_params: [],
      pos: tok_pos(start),
      is_builtin: False,
      exported: exported,
      imported: imported,
      module_path: module_path,
    ),
  ))
}

/// Procedure name: an atom or one of the operator tokens (`<`, `>`, `=<`,
/// `>=`, `=:=`, `=\=`, `=?=`, `@<`, `@>`, `@=<`, `@>=`, `=`, `=..`, `..=`,
/// `:=`).
fn parse_proc_name_token(ps: Ps) -> Result(#(Ps, Token), ParseError) {
  case peek(ps).token_type {
    lexer.TokAtom
    | lexer.Less
    | lexer.Greater
    | lexer.LessEqual
    | lexer.GreaterEqual
    | lexer.ArithEqual
    | lexer.ArithNotEqual
    | lexer.GroundEqual
    | lexer.AtLess
    | lexer.AtGreater
    | lexer.AtLessEqual
    | lexer.AtGreaterEqual
    | lexer.Equals
    | lexer.Univ
    | lexer.UnivDecompose
    | lexer.Assign -> Ok(advance(ps))
    _ -> fail_at("Expected procedure name", peek(ps))
  }
}

/// `social#agent` → module_path "social", name "agent";
/// `ui#actors#render` → module_path "ui#actors", name "render";
/// `merge` → no module path.
fn parse_imported_name_path(
  ps: Ps,
  first: String,
) -> Result(#(Ps, String, Option(String)), ParseError) {
  use #(ps, parts_rev) <- result.try(imported_path_loop(ps, [first]))
  // parts_rev is reversed: its head is the last component — the procedure
  // name; the remainder (re-reversed) is the module path.
  let assert [name, ..path_rev] = parts_rev
  case path_rev {
    [] -> Ok(#(ps, name, None))
    _ -> Ok(#(ps, name, Some(path_rev |> list.reverse |> string.join("#"))))
  }
}

fn imported_path_loop(
  ps: Ps,
  acc: List(String),
) -> Result(#(Ps, List(String)), ParseError) {
  let #(ps_hash, has_hash) = match_tok(ps, lexer.Hash)
  case has_hash {
    False -> Ok(#(ps, acc))
    True ->
      case check(ps_hash, lexer.TokAtom) {
        False ->
          fail_at(
            "Expected module path component or procedure name after \"#\"",
            peek(ps_hash),
          )
        True -> {
          let #(ps, token) = advance(ps_hash)
          imported_path_loop(ps, [token.lexeme, ..acc])
        }
      }
  }
}

/// Procedure argument type: `TypeName`, `TypeName?`, `_`, `_?`, qualified
/// `mod#TypeName[?]`, or parameterized `TypeName(Type1, …)[?]` (Dart
/// `_parseProcArgType`).
fn parse_proc_arg_type(ps: Ps) -> Result(#(Ps, TypeExpr), ParseError) {
  let pos = tok_pos(peek(ps))
  let #(ps_us, has_underscore) = match_tok(ps, lexer.Underscore)
  case has_underscore {
    // Primitive: _ or _?
    True -> {
      let #(ps, is_input) = match_tok(ps_us, lexer.Question)
      Ok(#(ps, type_ast.PrimitiveModeAlt(is_input, pos)))
    }
    False ->
      // Qualified type reference: atom # … # TypeName[?]
      case check(ps, lexer.TokAtom) && peek2(ps).token_type == lexer.Hash {
        True -> parse_qualified_type_ref(ps, pos)
        False ->
          case check(ps, lexer.TokVariable) || check(ps, lexer.TokReader) {
            True -> {
              let #(ps, token) = advance(ps)
              // Optional type arguments (recursive — nested parameterized
              // types are supported)
              let #(ps_args, has_args) = match_tok(ps, lexer.LParen)
              use #(ps, type_args) <- result.try(case has_args {
                False -> Ok(#(ps, []))
                True -> {
                  use #(ps, first) <- result.try(parse_proc_arg_type(ps_args))
                  use #(ps, args) <- result.try(comma_list(
                    ps,
                    [first],
                    parse_proc_arg_type,
                  ))
                  use #(ps, _) <- result.try(consume(
                    ps,
                    lexer.RParen,
                    "Expected \")\" after type arguments",
                  ))
                  Ok(#(ps, args))
                }
              })
              // Dart short-circuit: a READER token already carries the '?'
              // and no further QUESTION is consumed.
              let #(ps, is_input) = case token.token_type == lexer.TokReader {
                True -> #(ps, True)
                False -> match_tok(ps, lexer.Question)
              }
              Ok(#(
                ps,
                type_ast.TypeRef(token.lexeme, is_input, type_args, pos),
              ))
            }
            False -> fail_at("Expected type in procedure argument", peek(ps))
          }
      }
  }
}

fn parse_qualified_type_ref(
  ps: Ps,
  pos: Pos,
) -> Result(#(Ps, TypeExpr), ParseError) {
  let #(ps, path_parts_rev) = qualified_path_loop(ps, [])
  case check(ps, lexer.TokVariable) || check(ps, lexer.TokReader) {
    True -> {
      let #(ps, type_token) = advance(ps)
      let #(ps, is_input) = case type_token.token_type == lexer.TokReader {
        True -> #(ps, True)
        False -> match_tok(ps, lexer.Question)
      }
      let qualified_name =
        { path_parts_rev |> list.reverse |> string.join("#") }
        <> "#"
        <> type_token.lexeme
      Ok(#(ps, type_ast.TypeRef(qualified_name, is_input, [], pos)))
    }
    False ->
      fail_at(
        "Expected type name after module path in qualified type reference",
        peek(ps),
      )
  }
}

fn qualified_path_loop(ps: Ps, acc: List(String)) -> #(Ps, List(String)) {
  case check(ps, lexer.TokAtom) && peek2(ps).token_type == lexer.Hash {
    True -> {
      let #(ps, atom_token) = advance(ps)
      let #(ps, _) = advance(ps)
      qualified_path_loop(ps, [atom_token.lexeme, ..acc])
    }
    False -> #(ps, acc)
  }
}
