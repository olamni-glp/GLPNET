/*
 * SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
 * SPDX-License-Identifier: MIT
 *
 * Glp.g4 — ANTLR4 grammar for the GLP surface syntax (feature 065, US1/Scope A, task T010).
 *
 * §1.14 / Constitution IV-a: this grammar is a FAITHFUL description of the EXISTING accepted
 * syntax ONLY. Authored 2026-08-04 under Gabi + Udi approval (PROPOSAL-1.14.md). It is derived
 * strictly from the authoritative hand-written implementation — it invents no syntax:
 *   - Lexer:  out/csharp/lib/compiler/lexer.cs  (456 LOC) + token.cs (49 token types)
 *   - Parser: out/csharp/lib/compiler/parser.cs (authority) cross-checked vs
 *             glp_runtime/lib/compiler/parser.dart (line-for-line equivalent).
 * File:line citations to those sources appear inline. Where ANTLR's maximal-munch lexer or
 * ALL(*) parser cannot exactly reproduce a context-sensitive decision of the hand-written
 * recursive-descent scanner/parser, the divergence is marked  DIVERGENCE:  and carried into
 * REPORT.md as a feasibility finding (NOT a silent syntax change — SC-004).
 *
 * Additive spike artifact. The production parsers are untouched (FR-010).
 */
grammar Glp;

// Soft-keyword helper: match an ATOM by its lexeme without making it a lexer
// keyword (keeps tokenization identical to lexer.cs, which keywords only `mod`
// and `procedure`). Used by the directive/procDecl rules for module/stdlib/mode/
// user/system/exported/imported.
@parser::members {
    private bool SoftKw(string s) {
        var t = TokenStream.LT(1);
        return t != null && t.Type == ATOM && t.Text == s;
    }
}

/* ============================================================================
 * PARSER RULES
 * Mirrors the hand-written recursive descent: the four DISTINCT sub-grammars
 * (head / goal / goalOrGuard / typeAlt) are kept as separate rules exactly as
 * parser.cs keeps them separate — they accept genuinely different languages.
 * ==========================================================================*/

// Real entry point — ParseModule (parser.cs:58-327): directives then items.
// (The legacy Parse() entry, parser.cs:39-55, is module minus the typeDef/decl
//  items and is subsumed here.)
module : directive* item* EOF ;

item
    : procDecl
    | typeDef
    | clause
    ;

/* --- Directives (parser.cs:79-137). Only -module/-stdlib/-mode exist.
 * module/mode/stdlib/user/system are ATOMs (the lexer makes only `mod` and
 * `procedure` keywords, lexer.cs:237-246), matched here by lexeme via a
 * soft-keyword predicate so tokenization stays identical to the hand-lexer. */
// Common MINUS prefix factored out so each keyword predicate sits at the left
// edge of its inner alternative (ANTLR hoists gated predicates at the decision
// point; mid-rule predicates after a shared prefix do not disambiguate).
directive
    : MINUS
      ( {SoftKw("module")}? ATOM LPAREN moduleName RPAREN
      | {SoftKw("stdlib")}? ATOM
      | {SoftKw("mode")}?   ATOM LPAREN ({SoftKw("user")}? ATOM | {SoftKw("system")}? ATOM) RPAREN
      ) DOT
    ;

// Dotted module name (ParseModuleName, parser.cs:371-389).
moduleName : ATOM (DOT ATOM)* ;

/* --- Procedure declaration (ParseProcDeclaration, parser.cs:1718-1822).
 * `exported`/`imported` are ATOMs by lexeme (parser.cs:1725-1734). Parens
 * optional (nullary). `imported` may carry a #-path (parser.cs:1777-1797). */
procDecl
    : ({SoftKw("exported")}? ATOM | {SoftKw("imported")}? ATOM)? PROCEDURE
      procName (LPAREN argType (COMMA argType)* RPAREN)? DOT
    ;

// Procedure name may be an operator token (parser.cs:1747-1772, full set) or an
// atom; an imported name may be a #-qualified path (last component is the name).
procName
    : procNameToken (HASH ATOM)*
    ;
procNameToken
    : ATOM | LESS | GREATER | LESS_EQUAL | GREATER_EQUAL
    | ARITH_EQUAL | ARITH_NOT_EQUAL | GROUND_EQUAL
    | AT_LESS | AT_GREATER | AT_LESS_EQUAL | AT_GREATER_EQUAL
    | EQUALS | UNIV | UNIV_DECOMPOSE | ASSIGN
    ;

// Argument type (ParseProcArgType, parser.cs:1828-1889).
argType
    : UNDERSCORE QUESTION?                                                        // primitive mode
    | (ATOM HASH)+ (VARIABLE | READER) QUESTION?                                  // qualified TypeRef
    | (VARIABLE | READER) (LPAREN argType (COMMA argType)* RPAREN)? QUESTION?     // TypeRef (recursive)
    ;

/* --- Type definition (ParseTypeDef, parser.cs:1426-1468).
 * Name is VARIABLE|READER; optional VARIABLE params; `::=`; `;`-separated
 * alternatives; `.`-terminated. */
typeDef
    : (VARIABLE | READER) (LPAREN VARIABLE (COMMA VARIABLE)* RPAREN)?
      COLONCOLONEQ typeAlt (SEMICOLON typeAlt)* DOT
    ;

// Type alternative — a parallel term grammar (ParseTypeAlt*, parser.cs:1477-1711)
// that additionally permits a trailing `?` (explicit-dual marker) after any
// structure/list/paren/whole-alternative. Same operator table as `term`.
typeAlt : typeExpr QUESTION? ;

typeExpr
    : typeExpr (STAR | SLASH | SLASH_SLASH | MOD) typeExpr
    | typeExpr (PLUS | MINUS) typeExpr
    | typeExpr (LESS | GREATER | LESS_EQUAL | GREATER_EQUAL | EQUALS | ARITH_EQUAL | ARITH_NOT_EQUAL) typeExpr
    | typeExpr HASH typeExpr
    | typeExpr BACKSLASH typeExpr
    | typePrimary
    ;

typePrimary
    : MINUS typePrimary
    | (VARIABLE | READER) (LPAREN typeAlt (COMMA typeAlt)* RPAREN)? QUESTION?   // TypeName / TypeName(Args)
    | UNDERSCORE QUESTION?
    | NUMBER
    | STRING
    | typeList
    | LPAREN typeAlt (COMMA typeAlt)* RPAREN QUESTION?
    | ATOM (LPAREN typeAlt (COMMA typeAlt)* RPAREN)? QUESTION?                   // functor / constant
    ;

typeList
    : LBRACKET RBRACKET QUESTION?
    | LBRACKET typeAlt (COMMA typeAlt)* (PIPE typeAlt)? RBRACKET QUESTION?
    ;

/* --- Clause (ParseClause, parser.cs:493-545): HEAD :- GUARDS | BODY .
 *   - no `:-`         => fact           (head .)
 *   - `:-` .. no `|`  => head :- body   (all after `:-` are body goals)
 *   - `:-` G.. | B..  => head :- guards | body
 * `,` conjoins in both regions; `.` terminates (mandatory). */
clause
    : head (IMPLIES goalOrGuard (COMMA goalOrGuard)* (PIPE goal (COMMA goal)*)?)? DOT
    ;

/* --- Head (ParseAtom, parser.cs:778-861).
 * Leading-lvalue branch uniquely accepts UNDERSCORE (parser.cs:781) and all four
 * of := =.. ..= = . After a functor application, ONLY =.. and = are head infix
 * (parser.cs:842,851) — the ..= asymmetry is faithful. */
head
    : (VARIABLE | READER | UNDERSCORE) (ASSIGN term | UNIV term | UNIV_DECOMPOSE term | EQUALS term)
    | atomApp (UNIV term | EQUALS term)?
    ;

atomApp : ATOM (LPAREN term (COMMA term)* RPAREN)? ;

/* --- Body goal (ParseGoal, parser.cs:865-970).
 * Leading UNDERSCORE is NOT accepted here (parser.cs:869) — body goals cannot
 * start with `_`. A bare var must be followed by one of # := =.. ..= = . After a
 * functor: only # and =.. ; optional `@ atom` spawn (parser.cs:963-967). */
goal
    : (VARIABLE | READER) (HASH goal | ASSIGN term | UNIV term | UNIV_DECOMPOSE term | EQUALS term)
    | atomApp (HASH goal | UNIV term)? (AT ATOM)?
    ;

/* --- Guard-or-body predicate (ParseGoalOrGuard, parser.cs:548-768): the region
 * between `:-` and `|` (and body when no `|`). Richer than `goal`: allows a
 * leading `~` (negation), a parenthesized `;` disjunction, and infix comparison
 * guards whose operands are arithmetic-only (ParseExpression(6), parser.cs:741-762)
 * — so =?= and @<,@>,@=<,@>= are reachable ONLY here (they are absent from the
 * Pratt operator set, parser.cs:1201-1216). */
goalOrGuard
    : TILDE? goalOrGuardInner
    ;
goalOrGuardInner
    : LPAREN goalOrGuard (SEMICOLON goalOrGuard)? RPAREN
    | (VARIABLE | READER) (HASH goal | ASSIGN term | UNIV term | UNIV_DECOMPOSE term | EQUALS term)
    | atomApp (HASH goal | EQUALS term)? (AT ATOM)?
    | arith cmpOp arith
    ;

// Comparison operator set for the guard fallthrough (parser.cs:744-751).
cmpOp
    : LESS | GREATER | LESS_EQUAL | GREATER_EQUAL | EQUALS
    | ARITH_EQUAL | ARITH_NOT_EQUAL | GROUND_EQUAL
    | AT_LESS | AT_GREATER | AT_LESS_EQUAL | AT_GREATER_EQUAL
    ;

// Arithmetic-only operands for a comparison guard = ParseExpression(6): only the
// prec-20 and prec-10 operators, no comparison/#/\ (parser.cs:1008 min-prec).
arith
    : arith (STAR | SLASH | SLASH_SLASH | MOD) arith
    | arith (PLUS | MINUS) arith
    | arithPrimary
    ;
arithPrimary
    : (PLUS | MINUS | STAR | SLASH | SLASH_SLASH | MOD) LPAREN term (COMMA term)* RPAREN   // op-as-functor
    | MINUS arithPrimary
    | (VARIABLE | READER)
    | UNDERSCORE QUESTION?
    | NUMBER
    | STRING
    | list
    | LPAREN term (COMMA term)* RPAREN
    | ATOM (LPAREN term (COMMA term)* RPAREN)?
    ;

/* --- Term / expression (ParseTerm=ParseExpression, parser.cs:997-1013) — Pratt
 * table (parser.cs:1201-1252), all LEFT-associative, highest-binds-first:
 *   20: * / // mod   |   10: + -   |   5: < > =< >= = =:= =\=   |   2: #   |   1: \
 * ANTLR orders left-recursive alternatives high->low to reproduce it. */
term
    : term (STAR | SLASH | SLASH_SLASH | MOD) term
    | term (PLUS | MINUS) term
    | term (LESS | GREATER | LESS_EQUAL | GREATER_EQUAL | EQUALS | ARITH_EQUAL | ARITH_NOT_EQUAL) term
    | term HASH term
    | term BACKSLASH term
    | primary
    ;

/* --- Primary (ParsePrimary, parser.cs:1016-1198).
 * op-as-functor MUST precede unary-minus so `-(A,B)` is a struct not neg((A,B))
 * (parser.cs:1020-1041). A reader `?` is structurally legal ONLY on variables and
 * `_` (parser.cs:1081-1103) — NUMBER/STRING/struct/list with trailing `?` are a
 * hand-parser CompileError, so they are (faithfully) NOT accepted here either. */
primary
    : (PLUS | MINUS | STAR | SLASH | SLASH_SLASH | MOD) LPAREN term (COMMA term)* RPAREN
    | MINUS primary
    | (VARIABLE | READER) (ASSIGN term)?
    | UNDERSCORE QUESTION?
    | NUMBER
    | STRING
    | list
    | LPAREN term (COMMA term)* RPAREN
    | ATOM (LPAREN term (COMMA term)* RPAREN)?
    ;

// List (ParseList, parser.cs:1255-1325): right-assoc cons, optional `|` tail.
list
    : LBRACKET RBRACKET
    | LBRACKET term (COMMA term)* (PIPE term)? RBRACKET
    ;

/* ============================================================================
 * LEXER RULES  (from lexer.cs — order = maximal-munch priority, longest first)
 * ==========================================================================*/

// Whitespace + comments (SkipWhitespaceAndComments, lexer.cs:353-405).
WS            : [ \t\r\n]+            -> skip ;
LINE_COMMENT  : '%' ~[\r\n]*          -> skip ;
BLOCK_COMMENT : '/*' .*? '*/'         -> skip ;

// Multi-char operators — declared before their prefixes so ANTLR prefers the
// longer match, mirroring the hand-lexer's explicit Match() chains.
COLONCOLONEQ    : '::=' ;   // lexer.cs:169-174
IMPLIES         : ':-'  ;   // lexer.cs:178-181
ASSIGN          : ':='  ;   // lexer.cs:183-186
UNIV            : '=..' ;   // lexer.cs:125-130
UNIV_DECOMPOSE  : '..=' ;   // lexer.cs:53-58
GROUND_EQUAL    : '=?=' ;   // lexer.cs:157-164
ARITH_EQUAL     : '=:=' ;   // lexer.cs:139-144
ARITH_NOT_EQUAL : '=\\=' ;  // lexer.cs:148-153
LESS_EQUAL      : '=<'  ;   // lexer.cs:134-137
GREATER_EQUAL   : '>='  ;   // lexer.cs:118-121
AT_LESS_EQUAL   : '@=<' ;   // lexer.cs:92-98
AT_GREATER_EQUAL: '@>=' ;   // lexer.cs:82-88
AT_LESS         : '@<'  ;   // lexer.cs:77-80
AT_GREATER      : '@>'  ;   // lexer.cs:82-90
SLASH_SLASH     : '//'  ;   // lexer.cs:106-111

// Single-char tokens.
LPAREN    : '(' ;
RPAREN    : ')' ;
LBRACKET  : '[' ;
RBRACKET  : ']' ;
LBRACE    : '{' ;
RBRACE    : '}' ;
DOT       : '.' ;
COMMA     : ',' ;
PIPE      : '|' ;
QUESTION  : '?' ;
SEMICOLON : ';' ;
PLUS      : '+' ;
MINUS     : '-' ;
STAR      : '*' ;
SLASH     : '/' ;
LESS      : '<' ;
GREATER   : '>' ;
EQUALS    : '=' ;
TILDE     : '~' ;
HASH      : '#' ;
BACKSLASH : '\\' ;
AT        : '@' ;

// Keywords (only these two are keywords, lexer.cs:237-246).
// mod-functor (RESOLVED — feature 069 T016, Gabi + Udi approval 2026-08-11): the hand-lexer emits
// ATOM `mod` when the next char is `(` (a predicate call `mod(...)`), else keyword MOD
// (lexer.cs:237-246). A lexer semantic predicate reproduces this exactly — MOD matches only when the
// next input char is NOT `(`, so `mod(` fails this rule and falls through to ATOM (whose
// `[a-z][a-zA-Z0-9_]*` rule also matches `mod`), while `A mod B`, `mod)`, and a lone `mod` still lex
// as MOD. This is FAITHFUL tokenization of the EXISTING accepted syntax (§1.14 / IV-a): it changes no
// accepted syntax, only which token `mod(` produces, matching the hand-lexer. `InputStream.LA(1)` is
// the next char — the public Lexer.InputStream (IIntStream) property; the runtime's `_input` field is
// not accessible to a generated subclass in another assembly. See REPORT.md §7.
MOD       : 'mod' { InputStream.LA(1) != '(' }? ;
PROCEDURE : 'procedure' ;

// READER = a VARIABLE immediately followed by `?` (lexer.cs:250-258). Declared
// before VARIABLE/UNDERSCORE so `X?` munches to READER, while `X ?`, `foo?`, and
// `_?` fall to VARIABLE/ATOM + QUESTION exactly as the hand-lexer does.
READER
    : [A-Z] [a-zA-Z0-9_]* '?'
    | '_' [A-Z] [a-zA-Z0-9_]* '?'
    ;

// Variable: uppercase-initial, or `_` + uppercase-initial (named anonymous, e.g.
// _Out) — lexer.cs:250-251. A lone `_` (not alphanumeric-followed) is UNDERSCORE.
VARIABLE
    : [A-Z] [a-zA-Z0-9_]*
    | '_' [A-Z] [a-zA-Z0-9_]*
    ;

UNDERSCORE : '_' ;

// Number: optional leading `-` ONLY when glued to a digit (lexer.cs:202-211,266-293),
// so `5-3` => NUMBER(5) NUMBER(-3) and `-5` => NUMBER(-5), while `- 5`/`X - Y` keep
// MINUS. Optional fractional part.
NUMBER : '-'? [0-9]+ ('.' [0-9]+)? ;

// Atom: lowercase-initial identifier, or `_`-initial NOT followed by uppercase
// (e.g. reserved `_user`, or `_out`) — lexer.cs:190-262. Quoted atom `'...'`
// (single quotes) is also an ATOM usable as a functor (lexer.cs:343-346).
ATOM
    : [a-z] [a-zA-Z0-9_]*
    | '_' [a-z0-9_] [a-zA-Z0-9_]*
    | '\'' ( '\\' . | ~['\\] )* '\''
    ;

// String literal: double quotes (lexer.cs:347-350).
STRING : '"' ( '\\' . | ~["\\] )* '"' ;
